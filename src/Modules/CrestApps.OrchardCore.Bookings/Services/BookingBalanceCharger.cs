using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Collects a booking balance with the card its deposit was paid with, through the saved card charge service
/// (<see cref="ISavedPaymentMethodChargeService"/>): the balance's invoice carries the charge from the start
/// (<see cref="NewAutoCharge"/>), charged on its due date and, when declined, again a day later, three times in all;
/// after that the buyer pays from the invoice's pay link. This class keeps the balance in step with what the charges
/// came to and tells the booking payment handlers.
/// </summary>
public sealed class BookingBalanceCharger
{
    private readonly IBookingBalanceStore _balances;
    private readonly IBookingStore _bookings;
    private readonly ITransactionManager _transactions;
    private readonly ISavedPaymentMethodChargeService _charges;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IEnumerable<IBookingPaymentHandler> _paymentHandlers;
    private readonly global::YesSql.ISession _session;

    internal readonly IStringLocalizer S;

    public BookingBalanceCharger(
        IBookingBalanceStore balances,
        IBookingStore bookings,
        ITransactionManager transactions,
        IClock clock,
        ILogger<BookingBalanceCharger> logger,
        IStringLocalizer<BookingBalanceCharger> stringLocalizer,
        IEnumerable<IBookingPaymentHandler> paymentHandlers,
        ISavedPaymentMethodChargeService charges = null,
        global::YesSql.ISession session = null)
    {
        _session = session;
        _balances = balances;
        _bookings = bookings;
        _transactions = transactions;
        _charges = charges;
        _clock = clock;
        _logger = logger;
        _paymentHandlers = paymentHandlers;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the days between the balance's declined charges: charged again a day later, twice, so three times in all.
    /// </summary>
    public static int[] RetryDays
        => [1, 1];

    /// <summary>
    /// The charge a balance's invoice carries from the start: the card the deposit's checkout kept, on the due date,
    /// retried on <see cref="RetryDays"/>.
    /// </summary>
    /// <param name="depositSessionId">The checkout that paid the deposit and kept the card.</param>
    /// <param name="dueUtc">When the balance is due.</param>
    public static TransactionAutoCharge NewAutoCharge(string depositSessionId, DateTime dueUtc)
        => new()
        {
            SourceSessionId = depositSessionId,
            NextAttemptUtc = dueUtc,
            RetryDays = RetryDays,
        };

    /// <summary>
    /// Charges the balance now, such as when staff ask. A balance whose invoice carries no charge any more (it was
    /// handed to the buyer, or made before balances were charged this way) is given one first. Returns why it wasn't
    /// charged, or <see langword="null"/> when it was (or is settling).
    /// </summary>
    /// <param name="balance">The balance.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<string> ChargeAsync(BookingBalance balance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(balance);

        var transaction = await _transactions.FindByIdAsync(balance.TransactionId, cancellationToken);

        if (transaction is null)
        {
            return await FailAsync(balance, S["The balance's invoice could not be found."], cancellationToken);
        }

        if (await SettleIfClosedAsync(balance, transaction, cancellationToken))
        {
            return null;
        }

        if (_charges is null)
        {
            return S["Saved cards are charged through the Transactions feature. Enable it to charge one."];
        }

        await _balances.UpdateAsync(balance, cancellationToken);

        if (transaction.AutoCharge is null && await ScheduleAsync(balance, _clock.UtcNow, cancellationToken) is { } refused)
        {
            return refused;
        }

        var charged = await _charges.ChargeAsync(balance.TransactionId, cancellationToken);

        if (charged.ChargeStatus is SavedPaymentMethodChargeStatus.Succeeded or SavedPaymentMethodChargeStatus.Pending)
        {
            await SucceedAsync(balance, cancellationToken);

            return null;
        }

        var stored = await _balances.FindByIdAsync(balance.ItemId, cancellationToken) ?? balance;

        return stored.LastFailure ?? charged.Transaction?.AutoCharge?.LastFailure ?? string.Join(" ", charged.Errors);
    }

    /// <summary>
    /// Hands a due balance whose invoice carries no charge to the saved card charge service, such as one made before
    /// balances were charged this way, and settles one paid or canceled meanwhile. Returns whether it changed anything.
    /// </summary>
    /// <param name="balance">The due balance.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<bool> AdoptAsync(BookingBalance balance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(balance);

        var transaction = await _transactions.FindByIdAsync(balance.TransactionId, cancellationToken);

        if (transaction is null)
        {
            await FailAsync(balance, S["The balance's invoice could not be found."], cancellationToken);

            return true;
        }

        if (await SettleIfClosedAsync(balance, transaction, cancellationToken))
        {
            return true;
        }

        if (transaction.AutoCharge is not null || _charges is null)
        {
            return false;
        }

        await ScheduleAsync(balance, balance.NextAttemptUtc ?? _clock.UtcNow, cancellationToken);

        return true;
    }

    /// <summary>
    /// Records a charge of the balance the card declined, with the day it is charged again, and tells the booking payment
    /// handlers.
    /// </summary>
    /// <param name="transaction">The balance's invoice.</param>
    /// <param name="reason">Why the card declined.</param>
    /// <param name="nextAttemptUtc">When the card is charged again.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task RecordDeclinedAsync(Transaction transaction, string reason, DateTime nextAttemptUtc, CancellationToken cancellationToken = default)
    {
        if (await FindBalanceAsync(transaction, cancellationToken) is not { Status: BookingBalanceStatus.Scheduled } balance)
        {
            return;
        }

        balance.Attempts = transaction.AutoCharge?.Attempts ?? balance.Attempts + 1;
        balance.LastAttemptUtc = _clock.UtcNow;
        balance.LastFailure = reason;
        balance.NextAttemptUtc = nextAttemptUtc;

        await _balances.UpdateAsync(balance, cancellationToken);

        _logger.LogWarning("The balance of order '{OrderNumber}' wasn't charged and is charged again on {NextAttemptUtc:u}: {Reason}", balance.OrderNumber, nextAttemptUtc, reason);

        await SaveAsync(cancellationToken);
        await ReportFailureAsync(balance, reason, cancellationToken);
    }

    /// <summary>
    /// Records that the balance is no longer charged to the card (no retry left, or the card's bank asked the buyer to
    /// approve it): the buyer pays from the invoice's pay link. The booking payment handlers are told once.
    /// </summary>
    /// <param name="transaction">The balance's invoice.</param>
    /// <param name="reason">Why the last charge was not taken.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task RecordAbandonedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        if (await FindBalanceAsync(transaction, cancellationToken) is not { } balance)
        {
            return;
        }

        balance.Attempts++;
        balance.LastAttemptUtc = _clock.UtcNow;

        await FailAsync(balance, reason, cancellationToken);
        await SaveAsync(cancellationToken);
    }

    // The charge events come after the charge service committed its own changes, so what they change is committed here.
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> ScheduleAsync(BookingBalance balance, DateTime chargeOnUtc, CancellationToken cancellationToken)
    {
        var scheduled = await _charges.ScheduleAsync(new SavedPaymentMethodChargeRequest
        {
            TransactionId = balance.TransactionId,
            SourceSessionId = balance.DepositSessionId,
            ChargeOnUtc = chargeOnUtc,
            RetryDays = RetryDays,
        }, cancellationToken);

        if (scheduled.Succeeded)
        {
            return null;
        }

        return await FailAsync(balance, S["No card was kept when the deposit was paid. The buyer pays from the invoice's pay link."], cancellationToken);
    }

    // Paid some other way, such as from the pay link, or not owed any more.
    private async Task<bool> SettleIfClosedAsync(BookingBalance balance, Transaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.OutstandingAmount > 0m && transaction.Status is not (TransactionStatus.Paid or TransactionStatus.Canceled))
        {
            return false;
        }

        balance.Status = transaction.Status == TransactionStatus.Canceled ? BookingBalanceStatus.Canceled : BookingBalanceStatus.Paid;
        balance.NextAttemptUtc = null;

        await _balances.UpdateAsync(balance, cancellationToken);

        return true;
    }

    private async Task<BookingBalance> FindBalanceAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is null ||
            !string.Equals(transaction.ObligationId, BookingBalanceTenderProvider.BalanceObligationId, StringComparison.Ordinal) ||
            !string.Equals(transaction.Source, BookingsConstants.TransactionSource, StringComparison.Ordinal) ||
            await _balances.FindByOrderAsync(transaction.ReferenceId, cancellationToken) is not { } balance ||
            !string.Equals(balance.TransactionId, transaction.ItemId, StringComparison.Ordinal))
        {
            return null;
        }

        return balance;
    }

    private async Task SucceedAsync(BookingBalance balance, CancellationToken cancellationToken)
    {
        balance = await _balances.FindByIdAsync(balance.ItemId, cancellationToken) ?? balance;
        balance.Status = BookingBalanceStatus.Paid;
        balance.LastFailure = null;
        balance.NextAttemptUtc = null;

        await _balances.UpdateAsync(balance, cancellationToken);
    }

    private async Task<string> FailAsync(BookingBalance balance, string reason, CancellationToken cancellationToken)
    {
        // A balance that isn't scheduled any more, such as one that already failed for good, was paid or was let go of,
        // had its last failure reported already.
        var report = balance.Status == BookingBalanceStatus.Scheduled;

        balance.LastFailure = reason;
        balance.Status = BookingBalanceStatus.Failed;
        balance.NextAttemptUtc = null;

        await _balances.UpdateAsync(balance, cancellationToken);

        _logger.LogWarning("The balance of order '{OrderNumber}' wasn't charged: {Reason}", balance.OrderNumber, reason);

        if (report)
        {
            await ReportFailureAsync(balance, reason, cancellationToken);
        }

        return reason;
    }

    private async Task ReportFailureAsync(BookingBalance balance, string reason, CancellationToken cancellationToken)
    {
        var bookings = (await _bookings.ListByOrderAsync(balance.OrderId, cancellationToken))
            .Where(booking => booking.ConfirmedUtc.HasValue)
            .ToList();

        if (bookings.Count == 0)
        {
            return;
        }

        var context = new BookingPaymentContext
        {
            Bookings = bookings,
            OrderId = balance.OrderId,
            OrderNumber = balance.OrderNumber,
            PaymentKind = BookingPaymentKind.Balance,
            Amount = balance.Amount,
            Currency = balance.Currency,
            Reason = reason,
            IsFinal = balance.Status == BookingBalanceStatus.Failed,
            Reference = balance.TransactionId,
        };

        await _paymentHandlers.InvokeAsync((handler, paymentContext) => handler.PaymentFailedAsync(paymentContext, cancellationToken), context, _logger);
    }
}
