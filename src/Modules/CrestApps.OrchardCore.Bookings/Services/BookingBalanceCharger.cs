using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Charges a booking balance to the card its deposit was paid with, through the checkout, without the buyer there. A
/// declined card is tried again the next day, three times in all; after that the buyer pays from the invoice's pay link.
/// </summary>
public sealed class BookingBalanceCharger
{
    internal const int MaxAttempts = 3;

    private readonly IBookingBalanceStore _balances;
    private readonly IBookingStore _bookings;
    private readonly ITransactionManager _transactions;
    private readonly ICheckoutEngine _checkoutEngine;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly IEnumerable<ICheckoutSavedPaymentMethodProvider> _savedPaymentMethodProviders;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IEnumerable<IBookingPaymentHandler> _paymentHandlers;

    internal readonly IStringLocalizer S;

    public BookingBalanceCharger(
        IBookingBalanceStore balances,
        IBookingStore bookings,
        ITransactionManager transactions,
        ICheckoutEngine checkoutEngine,
        ICheckoutSessionStore sessionStore,
        IPaymentAttemptStore attemptStore,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders,
        IClock clock,
        ILogger<BookingBalanceCharger> logger,
        IStringLocalizer<BookingBalanceCharger> stringLocalizer,
        IEnumerable<IBookingPaymentHandler> paymentHandlers)
    {
        _balances = balances;
        _bookings = bookings;
        _transactions = transactions;
        _checkoutEngine = checkoutEngine;
        _sessionStore = sessionStore;
        _attemptStore = attemptStore;
        _savedPaymentMethodProviders = savedPaymentMethodProviders;
        _clock = clock;
        _logger = logger;
        _paymentHandlers = paymentHandlers;
        S = stringLocalizer;
    }

    /// <summary>
    /// Charges the balance now. Returns why it wasn't, or <see langword="null"/> when it was (or is settling).
    /// </summary>
    public async Task<string> ChargeAsync(BookingBalance balance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(balance);

        var now = _clock.UtcNow;
        var transaction = await _transactions.FindByIdAsync(balance.TransactionId, cancellationToken);

        if (transaction is null)
        {
            return await FailAsync(balance, S["The balance's invoice could not be found."], final: true, cancellationToken);
        }

        // Paid some other way, such as from the pay link, or not owed any more.
        if (transaction.OutstandingAmount <= 0m || transaction.Status is TransactionStatus.Paid or TransactionStatus.Canceled)
        {
            balance.Status = transaction.Status == TransactionStatus.Canceled ? BookingBalanceStatus.Canceled : BookingBalanceStatus.Paid;
            await _balances.UpdateAsync(balance, cancellationToken);

            return null;
        }

        // A charge already handed to the gateway is finished rather than repeated: charging again while it's settling is
        // how a buyer gets charged twice.
        var previous = await _sessionStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken: cancellationToken);

        if (previous is not null && previous.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending)
        {
            var pending = await _checkoutEngine.TryCompleteAsync(previous.SessionId, cancellationToken);

            if (pending.IsCompleted || pending.Status == CheckoutCompletionStatus.Pending)
            {
                return await SucceedAsync(balance, cancellationToken);
            }
        }

        if (previous is not null && previous.Status == CheckoutSessionStatus.Pending)
        {
            await _checkoutEngine.CancelAsync(previous.SessionId, "Replaced by a new charge.", cancellationToken);
        }

        if (await ReadSavedPaymentMethodAsync(balance, cancellationToken) is not { } paymentMethod)
        {
            return await FailAsync(balance, S["No card was kept when the deposit was paid. The buyer pays from the invoice's pay link."], final: true, cancellationToken);
        }

        var session = await _checkoutEngine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
            OwnerId = balance.OwnerId,
            Contact = new CheckoutContactInfo
            {
                DisplayName = balance.CustomerName,
                Email = balance.CustomerEmail,
            },
        }, cancellationToken);

        balance.Attempts++;
        balance.LastAttemptUtc = now;

        var outcome = await _checkoutEngine.BeginPaymentAsync(session.SessionId, new BeginPaymentOptions
        {
            ProviderKey = paymentMethod.ProviderKey,
            ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(paymentMethod),
        }, cancellationToken);

        if (outcome.Succeeded)
        {
            var completion = await _checkoutEngine.TryCompleteAsync(session.SessionId, cancellationToken);

            if (completion.IsCompleted || completion.Status == CheckoutCompletionStatus.Pending)
            {
                return await SucceedAsync(balance, cancellationToken);
            }

            return await FailAsync(balance, completion.ErrorMessage ?? S["The card was declined."], final: false, cancellationToken);
        }

        await _checkoutEngine.CancelAsync(session.SessionId, "The charge was not accepted.", cancellationToken);

        return await FailAsync(balance, outcome.ProviderErrorMessage ?? outcome.ErrorMessage ?? S["The card was declined."], final: false, cancellationToken);
    }

    private async Task<SavedPaymentMethod> ReadSavedPaymentMethodAsync(BookingBalance balance, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(balance.DepositSessionId))
        {
            return null;
        }

        foreach (var attempt in (await _attemptStore.GetBySessionAsync(balance.DepositSessionId, cancellationToken)).Where(attempt => attempt.State == PaymentAttemptState.Succeeded))
        {
            var provider = _savedPaymentMethodProviders.FirstOrDefault(candidate => string.Equals(candidate.Key, attempt.ProviderKey, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                continue;
            }

            try
            {
                if (await provider.GetSavedPaymentMethodAsync(attempt, cancellationToken) is { } paymentMethod)
                {
                    return paymentMethod;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the card kept by attempt '{AttemptId}' for the balance of order '{OrderNumber}'.", attempt.ItemId, balance.OrderNumber);
            }
        }

        return null;
    }

    private async Task<string> SucceedAsync(BookingBalance balance, CancellationToken cancellationToken)
    {
        balance = await ReloadAsync(balance, cancellationToken);
        balance.Status = BookingBalanceStatus.Paid;
        balance.LastFailure = null;
        balance.NextAttemptUtc = null;

        await _balances.UpdateAsync(balance, cancellationToken);

        return null;
    }

    // The checkout commits what it changed while charging, which leaves a document loaded before it detached: saving that
    // one would store it a second time. The stored one is read again and given what this charge changed.
    private async Task<BookingBalance> ReloadAsync(BookingBalance balance, CancellationToken cancellationToken)
    {
        if (await _balances.FindByIdAsync(balance.ItemId, cancellationToken) is not { } stored || ReferenceEquals(stored, balance))
        {
            return balance;
        }

        stored.Attempts = balance.Attempts;
        stored.LastAttemptUtc = balance.LastAttemptUtc;

        return stored;
    }

    private async Task<string> FailAsync(BookingBalance balance, string reason, bool final, CancellationToken cancellationToken)
    {
        balance = await ReloadAsync(balance, cancellationToken);

        // A balance that isn't scheduled any more, such as one that already failed for good, was paid or was let go of,
        // had its last failure reported already.
        var report = balance.Status == BookingBalanceStatus.Scheduled;

        balance.LastFailure = reason;

        if (final || balance.Attempts >= MaxAttempts)
        {
            balance.Status = BookingBalanceStatus.Failed;
            balance.NextAttemptUtc = null;
        }
        else
        {
            balance.NextAttemptUtc = _clock.UtcNow.AddDays(1);
        }

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
