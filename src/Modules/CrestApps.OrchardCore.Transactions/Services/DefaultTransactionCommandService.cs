using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users.Services;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionCommandService"/>. Every command runs under a distributed lock on the transaction
/// and saves before the lock is released, so two commands on the same transaction, on one node or several, never
/// overwrite each other. The rules for which command a transaction accepts come from <see cref="TransactionStateMachine"/>.
/// </summary>
public sealed class DefaultTransactionCommandService : ITransactionCommandService
{
    internal const string LockPrefix = "Transactions:Command:";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    private readonly ITransactionManager _transactionManager;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserService _userService;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly TransactionDisplayText _displayText;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionCommandService"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager, which raises the lifecycle events on save.</param>
    /// <param name="distributedLock">The lock that serializes commands on one transaction.</param>
    /// <param name="session">The session saved before the lock is released.</param>
    /// <param name="httpContextAccessor">The accessor for the signed-in user recorded as the actor.</param>
    /// <param name="userService">The user service used to name the actor.</param>
    /// <param name="displayNameProvider">The display name provider used to name the actor.</param>
    /// <param name="displayText">The words for stored values, used in timeline messages and to recognize a payment method that collected nothing.</param>
    /// <param name="serviceProvider">The service provider the optional services (payment handlers, reminders, refunds) are resolved from when a command needs them.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTransactionCommandService(
        ITransactionManager transactionManager,
        IDistributedLock distributedLock,
        ISession session,
        IHttpContextAccessor httpContextAccessor,
        IUserService userService,
        IDisplayNameProvider displayNameProvider,
        TransactionDisplayText displayText,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<DefaultTransactionCommandService> logger,
        IStringLocalizer<DefaultTransactionCommandService> stringLocalizer)
    {
        _transactionManager = transactionManager;
        _distributedLock = distributedLock;
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _userService = userService;
        _displayNameProvider = displayNameProvider;
        _displayText = displayText;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> RecordOfflinePaymentAsync(string transactionId, decimal amount, string reference, string note, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            if (amount <= 0m)
            {
                result.Fail(S["Enter a payment amount greater than zero."]);

                return null;
            }

            if (!TransactionStateMachine.CanRecordPayment(transaction))
            {
                result.Fail(S["This transaction is no longer collectable, so no payment was recorded."]);

                return null;
            }

            // Round at the currency's own scale so a zero-decimal currency (for example JPY) is never recorded with
            // fractions it cannot be paid in, and an over-payment never leaves a negative balance.
            var applied = CurrencyScale.Round(Math.Min(amount, transaction.OutstandingAmount), transaction.Currency);

            if (applied <= 0m)
            {
                result.Fail(S["The payment amount is too small to record against this transaction."]);

                return null;
            }

            var now = _clock.UtcNow;
            var trimmedReference = Trim(reference);

            var referenceSuffix = trimmedReference is null
                ? string.Empty
                : S[" Reference: {0}.", trimmedReference].Value;

            var noteSuffix = string.IsNullOrWhiteSpace(note)
                ? string.Empty
                : S[" Note: {0}", note].Value;

            var payment = TransactionPaymentHandlerExtensions.CreatePaymentEvent(
                now,
                applied,
                TransactionsConstants.SettlementMethods.Offline,
                S["An offline payment of {0} {1} was recorded.{2}{3}", CurrencyScale.Format(applied, transaction.Currency), transaction.Currency, referenceSuffix, noteSuffix].Value);

            payment.Reference = trimmedReference;
            await SetActorAsync(payment);

            transaction.Events.Add(payment);

            if (transaction.ApplyPayment(applied, TransactionsConstants.SettlementMethods.Offline, now, S["The transaction was fully paid and settled."].Value))
            {
                transaction.SettlementReference = trimmedReference ?? transaction.SettlementReference;
            }

            result.Event = payment;

            return () => RaisePaymentRecordedAsync(transaction, payment, cancellationToken);
        }, saveOnFailure: false, save: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> MarkPaidAsync(string transactionId, string note, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            if (!TransactionStateMachine.CanMarkPaid(transaction))
            {
                result.Fail(S["This transaction is no longer collectable, so it was not marked as paid."]);

                return null;
            }

            var now = _clock.UtcNow;

            // Marking paid settles whatever was still owed, so that remainder is the payment it records. What was
            // adjusted is not owed, so it is not counted as paid.
            var settled = transaction.OutstandingAmount;

            var message = string.IsNullOrWhiteSpace(note)
                ? S["The transaction was marked as paid."].Value
                : S["The transaction was marked as paid. Note: {0}", note].Value;

            var payment = TransactionPaymentHandlerExtensions.CreatePaymentEvent(now, settled, TransactionsConstants.SettlementMethods.Offline, message);

            await SetActorAsync(payment);

            transaction.Events.Add(payment);
            transaction.ApplyPayment(settled, TransactionsConstants.SettlementMethods.Offline, now, S["The transaction was fully paid and settled."].Value);
            result.Event = payment;

            if (settled <= 0m)
            {
                return null;
            }

            return () => RaisePaymentRecordedAsync(transaction, payment, cancellationToken);
        }, saveOnFailure: false, save: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> CancelAsync(string transactionId, string reason, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            if (!TransactionStateMachine.CanCancel(transaction))
            {
                result.Fail(S["This transaction has already reached a final state and cannot be canceled. Refund it instead if money was collected."]);

                return null;
            }

            var now = _clock.UtcNow;
            var trimmedReason = Trim(reason);

            transaction.Status = TransactionStatus.Canceled;
            transaction.UpdatedUtc = now;

            var canceled = new TransactionEvent
            {
                CreatedUtc = now,
                Type = TransactionEventType.Canceled,
                Message = trimmedReason is null
                    ? S["The transaction was canceled."].Value
                    : S["The transaction was canceled. Reason: {0}", trimmedReason].Value,
                Reason = trimmedReason,
            };

            await SetActorAsync(canceled);

            transaction.Events.Add(canceled);
            result.Event = canceled;

            return null;
        }, saveOnFailure: false, save: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> AdjustAsync(string transactionId, decimal amount, TransactionAdjustmentKind kind, string reason, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            if (!Enum.IsDefined(kind))
            {
                result.Fail(S["Choose why the amount is adjusted."]);

                return null;
            }

            if (amount <= 0m)
            {
                result.Fail(S["Enter an adjustment greater than zero."]);

                return null;
            }

            if (!TransactionStateMachine.CanAdjust(transaction))
            {
                result.Fail(S["This transaction owes nothing more, so it can't be adjusted."]);

                return null;
            }

            var adjusted = CurrencyScale.Round(amount, transaction.Currency);
            var outstanding = CurrencyScale.Round(transaction.OutstandingAmount, transaction.Currency);

            if (adjusted <= 0m)
            {
                result.Fail(S["The adjustment is too small to record against this transaction."]);

                return null;
            }

            // An adjustment lowers what is owed; taking off more than that would turn it into money owed back.
            if (adjusted > outstanding)
            {
                result.Fail(S["An adjustment can't be more than the {0} {1} still owed.", CurrencyScale.Format(outstanding, transaction.Currency), transaction.Currency]);

                return null;
            }

            var now = _clock.UtcNow;
            var trimmedReason = Trim(reason);
            var kindName = _displayText.AdjustmentKindName(kind);
            var formatted = $"{CurrencyScale.Format(adjusted, transaction.Currency)} {transaction.Currency}";

            transaction.AmountAdjusted = CurrencyScale.Round(transaction.AmountAdjusted + adjusted, transaction.Currency);
            transaction.UpdatedUtc = now;

            var adjustment = new TransactionEvent
            {
                Id = IdGenerator.GenerateId(),
                CreatedUtc = now,
                Type = TransactionEventType.Adjusted,
                Amount = adjusted,
                AdjustmentKind = kind,
                Reason = trimmedReason,
                Message = trimmedReason is null
                    ? S["{0} of {1} taken off what is owed.", kindName, formatted].Value
                    : S["{0} of {1} taken off what is owed. Reason: {2}", kindName, formatted, trimmedReason].Value,
            };

            await SetActorAsync(adjustment);

            transaction.Events.Add(adjustment);

            // Nothing left to pay: the transaction is settled and is never reminded again.
            if (transaction.TrySettle(now, S["Nothing is left to pay, so the transaction is settled."].Value))
            {
                transaction.SettlementMethod = TransactionsConstants.SettlementMethods.Adjusted;
            }

            result.Event = adjustment;

            return null;
        }, saveOnFailure: false, save: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> SendInvoiceAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            var reminderService = _serviceProvider.GetService<ITransactionReminderService>();

            if (reminderService is null)
            {
                result.Fail(S["Invoices are sent by the Transaction Reminders feature. Enable it to send invoices."]);

                return null;
            }

            if (transaction.AutoCollection is not null)
            {
                result.Fail(S["This transaction is charged automatically, so it isn't invoiced."]);

                return null;
            }

            if (!TransactionStateMachine.CanSendInvoice(transaction))
            {
                result.Fail(S["This transaction has no outstanding balance, so no invoice was sent."]);

                return null;
            }

            var invoiceNumber = transaction.InvoiceNumber;

            if (!await reminderService.SendInvoiceAsync(transaction, cancellationToken))
            {
                result.Fail(S["The invoice could not be sent. The owner may not have a notification channel configured."]);

                // An invoice number issued for a notice that could not be delivered is kept, so the next attempt sends
                // the same number.
                if (string.Equals(invoiceNumber, transaction.InvoiceNumber, StringComparison.Ordinal))
                {
                    return null;
                }

                return NoFollowUp;
            }

            result.Event = transaction.Events.LastOrDefault(evt => evt.Type == TransactionEventType.InvoiceSent);

            return null;
        }, saveOnFailure: true, save: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TransactionCommandResult> RefundAsync(string transactionId, decimal? amount, string reason, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(transactionId, async (transaction, result) =>
        {
            var refundService = _serviceProvider.GetService<ICheckoutRefundService>();
            var attemptStore = _serviceProvider.GetService<IPaymentAttemptStore>();
            var refundStore = _serviceProvider.GetService<IPaymentRefundStore>();

            if (refundService is null || attemptStore is null || refundStore is null)
            {
                result.Fail(S["Refunds are issued through the Checkout feature. Enable it to refund a transaction."]);

                return null;
            }

            if (amount is decimal requested && requested <= 0m)
            {
                result.Fail(S["Enter an amount greater than zero."]);

                return null;
            }

            var refundable = await GetRefundableAttemptsAsync(transaction, attemptStore, refundStore, cancellationToken);
            var totalRefundable = CurrencyScale.Round(refundable.Sum(entry => entry.Remaining), transaction.Currency);

            if (totalRefundable <= 0m)
            {
                result.Fail(S["Nothing paid online for this transaction can be refunded."]);

                return null;
            }

            var left = amount.HasValue
                ? CurrencyScale.Round(amount.Value, transaction.Currency)
                : totalRefundable;

            if (left > totalRefundable)
            {
                result.Fail(S["Only {0} {1} paid for this transaction can still be refunded.", CurrencyScale.Format(totalRefundable, transaction.Currency), transaction.Currency]);

                return null;
            }

            // Newest payment first, each up to what it still has to give back, until the amount is refunded.
            foreach (var (attempt, remaining) in refundable)
            {
                if (left <= 0m)
                {
                    break;
                }

                var portion = Math.Min(left, remaining);
                PaymentRefund refund;

                try
                {
                    refund = await refundService.RequestRefundAsync(
                        new RequestPaymentRefundContext
                        {
                            SessionId = attempt.SessionId,
                            OriginalTransactionId = attempt.TransactionId,
                            Amount = portion,
                            Reason = Trim(reason),
                        },
                        cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "The refund of payment attempt '{AttemptId}' for transaction '{TransactionId}' was refused.", attempt.ItemId, transaction.ItemId);

                    result.Fail(S["The refund of a payment couldn't be started. Another refund for it may still be in progress. Try again in a moment."]);

                    continue;
                }

                result.Refunds.Add(refund);

                if (refund.Status is RefundStatus.Failed or RefundStatus.Canceled)
                {
                    result.Fail(S["A refund of {0} {1} failed: {2}", CurrencyScale.Format(portion, transaction.Currency), transaction.Currency, refund.FailureReason ?? S["The provider did not say why."].Value]);

                    continue;
                }

                left = CurrencyScale.Round(left - portion, transaction.Currency);
            }

            if (left > 0m && result.Succeeded)
            {
                result.Fail(S["{0} {1} could not be refunded.", CurrencyScale.Format(left, transaction.Currency), transaction.Currency]);
            }

            // The refund handlers record each refund on the transaction and save it themselves.
            return NoFollowUp;
        }, saveOnFailure: true, save: false, cancellationToken);
    }

    // A follow-up that does nothing: the command changed something that must be saved, but has nothing to raise.
    private static Task NoFollowUp()
        => Task.CompletedTask;

    // Runs one command under the transaction's lock. The command validates and changes the transaction, and returns a
    // follow-up (such as telling the payment handlers) that runs only once the change is saved, or null when it has
    // nothing to do after the save. A refused command returns null and is not saved, unless it asks to be.
    private async Task<TransactionCommandResult> ExecuteAsync(
        string transactionId,
        Func<Transaction, TransactionCommandResult, Task<Func<Task>>> command,
        bool saveOnFailure,
        bool save,
        CancellationToken cancellationToken)
    {
        var result = new TransactionCommandResult();

        if (string.IsNullOrEmpty(transactionId))
        {
            return result.Fail(S["The transaction could not be found."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockPrefix + transactionId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return result.Fail(S["This transaction is being changed by someone else. Try again in a moment."]);
        }

        await using var _ = locker;

        var transaction = await _transactionManager.FindByIdAsync(transactionId, cancellationToken);

        if (transaction is null)
        {
            return result.Fail(S["The transaction could not be found."]);
        }

        result.Transaction = transaction;

        var followUp = await command(transaction, result);

        if (!result.Succeeded && !(saveOnFailure && followUp is not null))
        {
            return result;
        }

        try
        {
            if (save)
            {
                await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
            }

            // Saved before the lock is released, so a conflicting change made elsewhere is caught here rather than
            // silently overwritten when the request ends.
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("A concurrency conflict prevented a command on transaction '{TransactionId}'; another writer updated it first.", transaction.ItemId);

            return result.Fail(S["This transaction was changed by someone else while you were working on it. Reload it and try again."]);
        }

        if (followUp is not null)
        {
            await followUp();
        }

        return result;
    }

    // Resolved here rather than injected: a payment handler may itself start a payment through the checkout, and the
    // checkout is what applies payments, so injecting them would make the two depend on each other.
    private Task RaisePaymentRecordedAsync(Transaction transaction, TransactionEvent payment, CancellationToken cancellationToken)
        => _serviceProvider.GetServices<ITransactionPaymentHandler>().PaymentRecordedAsync(transaction, [payment], _logger, cancellationToken);

    // The confirmed payments made for the transaction that still have money to give back, newest first. They are found
    // both on the transaction's timeline (every payment applied online names its attempt) and by the attempts' own
    // reference, so a payment whose recording was interrupted is found too.
    private async Task<List<(PaymentAttempt Attempt, decimal Remaining)>> GetRefundableAttemptsAsync(
        Transaction transaction,
        IPaymentAttemptStore attemptStore,
        IPaymentRefundStore refundStore,
        CancellationToken cancellationToken)
    {
        var attempts = new Dictionary<string, PaymentAttempt>(StringComparer.Ordinal);

        foreach (var attempt in await attemptStore.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken))
        {
            attempts.TryAdd(attempt.ItemId, attempt);
        }

        foreach (var attemptId in transaction.Events
            .Where(evt => evt.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(evt.PaymentAttemptId))
            .Select(evt => evt.PaymentAttemptId)
            .Distinct(StringComparer.Ordinal))
        {
            if (attempts.ContainsKey(attemptId))
            {
                continue;
            }

            var attempt = await attemptStore.FindByIdAsync(attemptId, cancellationToken);

            if (attempt is not null)
            {
                attempts[attempt.ItemId] = attempt;
            }
        }

        var refundable = new List<(PaymentAttempt Attempt, decimal Remaining)>();

        foreach (var attempt in attempts.Values.OrderByDescending(attempt => attempt.CreatedUtc))
        {
            // Only a settled payment with a real gateway transaction can be refunded, and a promise to pay later
            // collected nothing to give back.
            if (attempt.State != PaymentAttemptState.Succeeded ||
                string.IsNullOrEmpty(attempt.TransactionId) ||
                _displayText.FindPaymentProvider(attempt.ProviderKey)?.Capabilities?.DefersPayment == true)
            {
                continue;
            }

            var refunds = (await refundStore.GetByOriginalTransactionAsync(attempt.TransactionId, cancellationToken)).ToArray();

            // A refund waiting to be settled by hand must be closed before another one starts for the same payment.
            if (refunds.Any(refund => refund.Status == RefundStatus.PendingManualReview))
            {
                continue;
            }

            // A refund still in flight counts: ignoring it would refund the same money twice.
            var alreadyRefunded = refunds
                .Where(refund => refund.Status is not (RefundStatus.Failed or RefundStatus.Canceled))
                .Sum(refund => refund.RefundGrossAmount);

            var remaining = CurrencyScale.Round(attempt.ConfirmedAmount + attempt.ConfirmedTaxAmount - alreadyRefunded, transaction.Currency);

            if (remaining > 0m)
            {
                refundable.Add((attempt, remaining));
            }
        }

        return refundable;
    }

    private async Task SetActorAsync(TransactionEvent transactionEvent)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        transactionEvent.ActorId = userId;

        var owner = await _userService.GetUserByUniqueIdAsync(userId);

        transactionEvent.ActorName = owner is null
            ? user.Identity?.Name
            : await _displayNameProvider.GetAsync(owner);
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
