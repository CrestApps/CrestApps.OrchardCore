using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Models;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Pays an order's bookings by deposit: what the deposit doesn't cover is owed on an invoice on the ledger, due some days
/// before the earliest booking, with its reminders and pay link, and charged then to the card the deposit is paid with.
/// Refunding what's owed takes it off the invoice.
/// </summary>
public sealed class BookingBalanceTenderProvider : DeferredBalanceTenderProviderBase
{
    // The obligation an order's balance invoice is recorded under, so completing a checkout twice owes once.
    internal const string BalanceObligationId = "booking-balance";

    private readonly BookingDepositService _deposits;
    private readonly IBookingBalanceStore _balances;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOrderStore _orders;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public BookingBalanceTenderProvider(
        BookingDepositService deposits,
        IBookingBalanceStore balances,
        ITransactionManager transactions,
        ITransactionCommandService commands,
        IServiceProvider serviceProvider,
        IOrderStore orders,
        IClock clock,
        ILocalClock localClock,
        ILogger<BookingBalanceTenderProvider> logger,
        IStringLocalizer<BookingBalanceTenderProvider> stringLocalizer)
        : base(transactions, commands, clock, logger, stringLocalizer)
    {
        _deposits = deposits;
        _balances = balances;
        _serviceProvider = serviceProvider;
        _orders = orders;
        _clock = clock;
        _localClock = localClock;
        _logger = logger;
        S = stringLocalizer;
    }

    public override string Key
        => BookingsConstants.BalanceTenderKey;

    // The balance is charged later to the card this checkout keeps.
    public override bool PaysWithSavedPaymentMethod
        => true;

    protected override string ObligationId
        => BalanceObligationId;

    public override async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = await _deposits.GetOrderAsync(context.Flow, cancellationToken);
        var offer = await _deposits.GetOfferAsync(order, context.Invoice, cancellationToken);

        if (offer is null)
        {
            return [];
        }

        // Only this checkout's order's balance, whatever the checkout was told.
        var reference = BookingDepositService.ToReference(order.ItemId, context.Flow.Session.SessionId);

        if (!context.References.Contains(reference, StringComparer.Ordinal))
        {
            return [];
        }

        return
        [
            new TenderLine
            {
                Reference = reference,
                // The offer's due date is already the site's own date.
                Description = S["Balance, due {0:d}", offer.DueDate],
                Amount = offer.Balance,
            },
        ];
    }

    public override async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var (orderId, sessionId) = BookingDepositService.FromReference(reference);

        if (string.IsNullOrEmpty(sessionId) ||
            await _serviceProvider.GetRequiredService<ICheckoutSessionStore>().GetAsync(sessionId, cancellationToken) is not { } session ||
            !session.TryGet<CheckoutInvoice>(out var invoice) ||
            await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order ||
            !string.Equals(order.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // What's left once the balances apply was taken off; the offer is on the whole amount.
        var whole = invoice.InitialPaymentAmount + (invoice.Tenders ?? []).Sum(tender => tender.Amount);

        return (await _deposits.GetOfferAsync(order, new CheckoutInvoice { InitialPaymentAmount = whole, Currency = invoice.Currency }, cancellationToken))?.Balance;
    }

    protected override string GetSessionId(string reference)
        => BookingDepositService.FromReference(reference).SessionId;

    protected override async Task<DeferredBalanceDebit> PrepareDebitAsync(string reference, CheckoutSession session, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var (orderId, _) = BookingDepositService.FromReference(reference);

        if (await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order)
        {
            return null;
        }

        var offer = await _deposits.GetOfferAsync(order, new CheckoutInvoice { InitialPaymentAmount = amount + 1m, Currency = currency }, cancellationToken);
        var dueUtc = offer?.DueUtc ?? _clock.UtcNow.AddDays(1);

        return new DeferredBalanceDebit
        {
            Title = S["Order {0}, balance", order.OrderNumber].Value,
            Source = BookingsConstants.TransactionSource,
            DueUtc = dueUtc,
            Message = S["The rest of the bookings' price after the deposit, charged to the card on {0:d}.", (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc))).DateTime].Value,
            State = order,
        };
    }

    protected override async Task DebitedAsync(Transaction transaction, DeferredBalanceDebit debit, CheckoutSession session, CancellationToken cancellationToken)
    {
        var order = (Order)debit.State;

        await _balances.CreateAsync(new BookingBalance
        {
            ItemId = global::OrchardCore.IdGenerator.GenerateId(),
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            TransactionId = transaction.ItemId,
            DepositSessionId = session.SessionId,
            OwnerId = session.OwnerId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            DueUtc = debit.DueUtc,
            Status = BookingBalanceStatus.Scheduled,
            CreatedUtc = transaction.CreatedUtc,
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The balance of order '{OrderNumber}', {Amount}, is due {DueUtc:u}.", order.OrderNumber, ProductPriceFormatter.Format(transaction.Amount, transaction.Currency), debit.DueUtc);
        }
    }

    // Once nothing is owed any more, the charge scheduled for the due date is canceled.
    protected override async Task CreditedAsync(string reference, Transaction transaction, decimal adjustment, CancellationToken cancellationToken)
    {
        var (orderId, _) = BookingDepositService.FromReference(reference);

        if (adjustment >= transaction.OutstandingAmount && await _balances.FindByOrderAsync(orderId, cancellationToken) is { Status: BookingBalanceStatus.Scheduled } balance)
        {
            balance.Status = BookingBalanceStatus.Canceled;
            await _balances.UpdateAsync(balance, cancellationToken);
        }
    }
}
