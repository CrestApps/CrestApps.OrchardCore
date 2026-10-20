using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// Rings up a sale at a register: an order of the register's store, its stock held there, and a checkout taxed by the
/// store's address that staff pay in cash, by card or with a gift card, then complete.
/// </summary>
public sealed class PosSaleService
{
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly ICheckoutEngine _engine;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly CheckoutTenderChoices _tenders;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public PosSaleService(
        ICartService cartService,
        IOrderService orderService,
        ICheckoutEngine engine,
        ICheckoutSessionStore sessionStore,
        CheckoutInvoiceBuilder invoiceBuilder,
        CheckoutTenderChoices tenders,
        ISession session,
        IClock clock,
        IStringLocalizer<PosSaleService> stringLocalizer)
    {
        _cartService = cartService;
        _orderService = orderService;
        _engine = engine;
        _sessionStore = sessionStore;
        _invoiceBuilder = invoiceBuilder;
        _tenders = tenders;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Rings up the lines at a register in its open shift, for a known buyer or a walk-in one.
    /// </summary>
    public async Task<PosSaleResult> StartAsync(Register register, RegisterShift shift, IReadOnlyList<PosSaleLine> lines, PosBuyer buyer, ClaimsPrincipal staff, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(shift);

        var now = _clock.UtcNow;

        // Priced as a cart is, so the store's prices, sales and volume prices apply, without keeping a cart.
        var cart = new Cart
        {
            ItemId = UniqueId.GenerateId(),
            UserId = buyer?.UserId,
            Currency = register.Currency,
            Lines = (lines ?? [])
                .Where(line => line.Quantity > 0 && !string.IsNullOrEmpty(line.ProductId))
                .Select(line => new CartLine
                {
                    LineId = UniqueId.GenerateId(),
                    ContentItemId = line.ProductId,
                    PriceId = string.IsNullOrEmpty(line.PriceId) ? null : line.PriceId,
                    VariantId = string.IsNullOrEmpty(line.VariantId) ? null : line.VariantId,
                    Quantity = line.Quantity,
                    AddOns = line.AddOns is { Count: > 0 } addOns ? new Dictionary<string, string>(addOns) : null,
                    AddedUtc = now,
                })
                .ToList(),
            CreatedUtc = now,
            ModifiedUtc = now,
            ExpiresUtc = now.AddHours(1),
        };

        if (cart.Lines.Count == 0)
        {
            return PosSaleResult.Fail(S["Ring up at least one product."]);
        }

        var pricing = await _cartService.PriceAsync(cart, cancellationToken);

        if (!pricing.CanCheckout)
        {
            return PosSaleResult.Fail([.. pricing.Lines.Where(line => line.Problem is not null).Select(line => $"{line.Title}: {line.Problem.Value}")]);
        }

        var created = await _orderService.CreateFromCartAsync(pricing, order =>
        {
            order.Channel = OrderChannels.PointOfSale;
            order.LocationId = register.LocationId;
            order.RegisterId = register.ItemId;
            order.ShiftId = shift.ItemId;
            order.StaffUserId = staff.FindFirstValue(ClaimTypes.NameIdentifier);
            order.StaffName = PosShiftService.GetName(staff);
            order.CartId = null;

            if (buyer is not null)
            {
                order.UserId = buyer.UserId;
                order.CustomerId = buyer.CustomerId;
                order.CustomerName = buyer.Name;
                order.CustomerEmail = buyer.Email;
                order.CustomerPhone = buyer.Phone;
            }

            order.BillingAddress = register.Address;
        }, cancellationToken);

        // The last units may have just sold at another register or online.
        if (!created.Succeeded)
        {
            return PosSaleResult.Fail([.. created.Problems]);
        }

        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await _session.CancelAsync();

            return PosSaleResult.Fail(S["The same items are being sold elsewhere right now. Ring the sale up again."]);
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = created.Order.ItemId,
        }, cancellationToken);

        // Taken away over the counter: nothing ships, and it's taxed where it's sold.
        session.Put(new CheckoutCounterSale
        {
            RegisterId = register.ItemId,
            ShiftId = shift.ItemId,
            LocationId = register.LocationId,
        });
        session.Put(new CheckoutShippingInfo
        {
            Address = register.Address,
            RateId = "in-store",
            Name = S["In store"],
            Amount = 0m,
            LocationId = register.LocationId,
        });

        if (buyer is not null && !string.IsNullOrEmpty(buyer.Email))
        {
            session.Put(new CheckoutContactInfo
            {
                DisplayName = buyer.Name,
                Email = buyer.Email,
                Phone = buyer.Phone,
            });
        }

        await _invoiceBuilder.BuildAsync(new CheckoutFlow(session));
        await _sessionStore.SaveAsync(session, cancellationToken);

        return new PosSaleResult { Session = session, Order = created.Order };
    }

    /// <summary>
    /// Pays a sale: cash handed over, then, for what's left, a card on the counter terminal or on a Stripe reader; and
    /// completes it once nothing is left to pay. A reader's payment completes once the buyer presents their card, so
    /// paying again with nothing asks whether it has.
    /// </summary>
    public async Task<PosPaymentResult> PayAsync(CheckoutSession session, decimal? cash, bool card, string approvalCode, string readerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var flow = new CheckoutFlow(session);

        if (cash is > 0m && session.TryGet<CheckoutInvoice>(out var current))
        {
            await _tenders.AddAsync(flow, PosConstants.CashTenderKey, PosCashTenderProvider.ToReference(cash.Value, current.Currency));
            await _sessionStore.SaveAsync(session, cancellationToken);
        }

        if (!session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return PosPaymentResult.Fail(S["The sale has no invoice; ring it up again."]);
        }

        var paymentBegun = session.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending;

        if (invoice.AmountDue > 0m && (card || !string.IsNullOrWhiteSpace(readerId)))
        {
            var outcome = await _engine.BeginPaymentAsync(session.SessionId, string.IsNullOrWhiteSpace(readerId)
                ? new BeginPaymentOptions
                {
                    ProviderKey = PosConstants.CounterCardProviderKey,
                    ProviderData = string.IsNullOrWhiteSpace(approvalCode) ? null : new Dictionary<string, string> { ["approvalCode"] = approvalCode.Trim() },
                }
                : new BeginPaymentOptions
                {
                    ProviderKey = PosConstants.TerminalProviderKey,
                    ProviderData = new Dictionary<string, string> { ["readerId"] = readerId.Trim() },
                }, cancellationToken);

            if (!outcome.Succeeded)
            {
                return PosPaymentResult.Fail(outcome.ErrorMessage);
            }
        }
        else if (invoice.AmountDue > 0m && !paymentBegun)
        {
            return new PosPaymentResult { Status = PosPaymentStatus.Due, AmountDue = invoice.AmountDue, Change = GetChange(session, invoice) };
        }

        var result = await _engine.TryCompleteAsync(session.SessionId, cancellationToken);

        if (result.IsCompleted)
        {
            return new PosPaymentResult { Status = PosPaymentStatus.Completed, Change = GetChange(session, invoice), CardAmount = invoice.AmountDue };
        }

        // The reader is waiting for the card, or the bank for the reader.
        return result.Status == CheckoutCompletionStatus.Pending
            ? new PosPaymentResult { Status = PosPaymentStatus.Pending, AmountDue = invoice.AmountDue, Change = GetChange(session, invoice) }
            : PosPaymentResult.Fail(result.ErrorMessage ?? S["The sale couldn't be completed: {0}.", result.Status]);
    }

    /// <summary>
    /// Returns the change to hand back: the cash handed over less what it paid.
    /// </summary>
    public static decimal GetChange(CheckoutSession session, CheckoutInvoice invoice)
    {
        var handedOver = session.TryGet<CheckoutTenderSelection>(out var selection)
            ? selection.For(PosConstants.CashTenderKey).Sum(reference => PosCashTenderProvider.FromReference(reference, invoice.Currency) ?? 0m)
            : 0m;
        var kept = (invoice.Tenders ?? []).Where(tender => tender.ProviderKey == PosConstants.CashTenderKey).Sum(tender => tender.Amount);

        return Math.Max(0m, handedOver - kept);
    }
}

/// <summary>
/// A product rung up.
/// </summary>
public sealed class PosSaleLine
{
    public string ProductId { get; set; }

    public string VariantId { get; set; }

    public string PriceId { get; set; }

    public int Quantity { get; set; } = 1;

    public Dictionary<string, string> AddOns { get; set; }
}

/// <summary>
/// The buyer of a sale, when staff know them: their customer record, and the account their points follow.
/// </summary>
public sealed class PosBuyer
{
    public string CustomerId { get; set; }

    public string UserId { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }
}

public sealed class PosSaleResult
{
    public CheckoutSession Session { get; init; }

    public Order Order { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool Succeeded
        => Session is not null && Problems.Count == 0;

    internal static PosSaleResult Fail(params string[] problems)
        => new() { Problems = problems };
}

public enum PosPaymentStatus
{
    /// <summary>
    /// Paid and done: the order is placed.
    /// </summary>
    Completed,

    /// <summary>
    /// Some is still to pay, by card or more cash.
    /// </summary>
    Due,

    /// <summary>
    /// A card reader is taking the payment: pay again with nothing to ask whether it's done.
    /// </summary>
    Pending,

    Failed,
}

public sealed class PosPaymentResult
{
    public PosPaymentStatus Status { get; init; }

    public decimal AmountDue { get; init; }

    public decimal Change { get; init; }

    public decimal CardAmount { get; init; }

    public string Error { get; init; }

    internal static PosPaymentResult Fail(string error)
        => new() { Status = PosPaymentStatus.Failed, Error = error };
}
