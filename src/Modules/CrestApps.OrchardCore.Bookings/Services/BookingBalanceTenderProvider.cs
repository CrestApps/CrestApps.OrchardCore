using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
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
public sealed class BookingBalanceTenderProvider : ICheckoutTenderProvider
{
    // The obligation an order's balance invoice is recorded under, so completing a checkout twice owes once.
    internal const string ObligationId = "booking-balance";

    private readonly BookingDepositService _deposits;
    private readonly IBookingBalanceStore _balances;
    private readonly ITransactionManager _transactions;
    private readonly ITransactionCommandService _commands;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOrderStore _orders;
    private readonly IClock _clock;
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
        ILogger<BookingBalanceTenderProvider> logger,
        IStringLocalizer<BookingBalanceTenderProvider> stringLocalizer)
    {
        _deposits = deposits;
        _balances = balances;
        _transactions = transactions;
        _commands = commands;
        _serviceProvider = serviceProvider;
        _orders = orders;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Key
        => BookingsConstants.BalanceTenderKey;

    // The balance is charged later to the card this checkout keeps.
    public bool PaysWithSavedPaymentMethod
        => true;

    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
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
                Description = S["Balance, due {0:d}", offer.DueUtc],
                Amount = offer.Balance,
            },
        ];
    }

    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
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

    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var (orderId, sessionId) = BookingDepositService.FromReference(reference);

        if (flow?.Session is not CheckoutSession session ||
            !string.Equals(session.SessionId, sessionId, StringComparison.Ordinal) ||
            await _orders.FindByIdAsync(orderId, cancellationToken) is not { } order)
        {
            throw new InvalidOperationException($"The booking balance '{reference}' doesn't belong to this checkout.");
        }

        if (await _transactions.GetByObligationAsync(session.SessionId, ObligationId, cancellationToken) is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var offer = await _deposits.GetOfferAsync(order, new CheckoutInvoice { InitialPaymentAmount = amount + 1m, Currency = currency }, cancellationToken);
        var dueUtc = offer?.DueUtc ?? now.AddDays(1);
        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);

        transaction.Title = S["Order {0}, balance", order.OrderNumber].Value;
        transaction.Source = BookingsConstants.TransactionSource;
        transaction.OwnerId = session.OwnerId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = session.ReferenceType;
        transaction.ReferenceId = session.ReferenceId;
        transaction.ReferenceVersionId = session.ReferenceVersionId;
        transaction.CheckoutSessionId = session.SessionId;
        transaction.ObligationId = ObligationId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.AmountPaid = 0m;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = dueUtc;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = S["The rest of the bookings' price after the deposit, charged to the card on {0:d}.", dueUtc].Value,
        });

        await _transactions.CreateAsync(transaction, cancellationToken);

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
            Amount = amount,
            Currency = currency,
            DueUtc = dueUtc,
            Status = BookingBalanceStatus.Scheduled,
            CreatedUtc = now,
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The balance of order '{OrderNumber}', {Amount}, is due {DueUtc:u}.", order.OrderNumber, ProductPriceFormatter.Format(amount, currency), dueUtc);
        }
    }

    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        var (orderId, sessionId) = BookingDepositService.FromReference(reference);

        if (amount <= 0m || string.IsNullOrEmpty(sessionId) ||
            await _transactions.GetByObligationAsync(sessionId, ObligationId, cancellationToken) is not { } transaction)
        {
            return;
        }

        // What's refunded comes off what's still owed; what was already paid on the invoice is refunded by the store.
        var adjustment = Math.Min(amount, transaction.OutstandingAmount);

        if (adjustment <= 0m)
        {
            _logger.LogWarning("A refund of {Amount} went to the balance of checkout '{SessionId}', which was already paid; refund it directly.", amount, sessionId);

            return;
        }

        var result = await _commands.AdjustAsync(transaction.ItemId, adjustment, TransactionAdjustmentKind.Waiver, string.IsNullOrWhiteSpace(reason) ? S["Refunded"].Value : reason, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The balance invoice '{TransactionId}' couldn't be lowered by a refund: {Errors}", transaction.ItemId, string.Join(" ", result.Errors));
        }
        else if (adjustment >= transaction.OutstandingAmount && await _balances.FindByOrderAsync(orderId, cancellationToken) is { Status: BookingBalanceStatus.Scheduled } balance)
        {
            balance.Status = BookingBalanceStatus.Canceled;
            await _balances.UpdateAsync(balance, cancellationToken);
        }
    }
}
