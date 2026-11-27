using CrestApps.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Refuses to take payment for a settlement checkout whose transactions no longer owe what it billed when it started,
/// or whose transactions another checkout is paying right now.
/// </summary>
/// <remarks>
/// A pay link opened in two tabs, or posted twice, starts two checkouts of the same transaction, and both bill what was
/// owed then. The check runs under the transaction's lock (<see cref="GetLockKey"/>), and a payment that passes leaves a
/// <see cref="TransactionSettlementHold"/> on the transaction, committed before the lock is let go. The other checkout's
/// payment is refused while the holder's payment may still take the money, as <see cref="ICheckoutPaymentHoldService"/>
/// decides, and a part payment applied leaves the next part free to pay.
/// </remarks>
public sealed class TransactionSettlementPaymentGate : ICheckoutPaymentGate
{
    /// <summary>
    /// How long a hold whose checkout has no payment attempt yet keeps others out (<see cref="CheckoutPaymentHold.BeginWindow"/>).
    /// </summary>
    public static readonly TimeSpan BeginWindow = CheckoutPaymentHold.BeginWindow;

    /// <summary>
    /// How long a payment that was begun but not finished keeps other checkouts of the transaction out
    /// (<see cref="CheckoutPaymentHold.PendingHoldLimit"/>).
    /// </summary>
    public static readonly TimeSpan PendingHoldLimit = CheckoutPaymentHold.PendingHoldLimit;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ITransactionManager _transactions;
    private readonly ICheckoutPaymentHoldService _holds;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementPaymentGate"/> class.
    /// </summary>
    /// <param name="transactions">The transaction manager.</param>
    /// <param name="holds">Tells whether another checkout's payment for a transaction may still take the money.</param>
    /// <param name="distributedLock">The lock each transaction is checked and held under.</param>
    /// <param name="session">The YesSql session the hold is committed with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementPaymentGate(
        ITransactionManager transactions,
        ICheckoutPaymentHoldService holds,
        IDistributedLock distributedLock,
        ISession session,
        IStringLocalizer<TransactionSettlementPaymentGate> stringLocalizer)
    {
        _transactions = transactions;
        _holds = holds;
        _distributedLock = distributedLock;
        _session = session;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the key of the lock a transaction's payments are checked under.
    /// </summary>
    /// <param name="transactionId">The transaction's id.</param>
    public static string GetLockKey(string transactionId)
        => "TRANSACTION_SETTLE_" + transactionId;

    /// <inheritdoc />
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var billed = session.Steps
            .Where(step => string.Equals(step.Key, TransactionsConstants.SettlementStepKey, StringComparison.Ordinal))
            .SelectMany(step => step.BillingItems ?? [])
            .Where(item => !string.IsNullOrEmpty(item.ItemId))
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ToList();

        if (billed.Count == 0)
        {
            return;
        }

        // Taken in the same order by every checkout, so two that pay overlapping transactions can't each wait on the other.
        var lockers = new List<ILocker>();

        try
        {
            foreach (var item in billed)
            {
                var (locker, locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(item.ItemId), _lockTimeout, _lockExpiration);

                if (!locked)
                {
                    context.Refuse(S["Another payment for this is being made right now. Please try again in a moment."]);

                    return;
                }

                lockers.Add(locker);
            }

            await CheckAndHoldAsync(context, session, billed, cancellationToken);
        }
        finally
        {
            foreach (var locker in lockers)
            {
                await locker.DisposeAsync();
            }
        }
    }

    private async Task CheckAndHoldAsync(CheckoutPaymentBeginningContext context, CheckoutSession session, List<BillingItem> billed, CancellationToken cancellationToken)
    {
        // A payer of one transaction may have asked to pay part of it; several transactions are always paid in full.
        var requested = billed.Count == 1 && session.TryGet<CheckoutRequestedAmount>(out var requestedAmount)
            ? requestedAmount.Amount
            : (decimal?)null;

        var transactions = new List<Transaction>();

        foreach (var item in billed)
        {
            var transaction = await _transactions.FindByIdAsync(item.ItemId, cancellationToken);

            // The amount is worked out again the way the checkout did when it started: the whole balance, or the part the
            // payer asked for when the transaction takes part payments. Anything else means it was paid or changed since.
            if (transaction is null ||
                transaction.Status is TransactionStatus.Paid or TransactionStatus.Canceled ||
                !TransactionStateMachine.TryGetPaymentAmount(transaction, requested, out var owed) ||
                owed != item.Amount)
            {
                context.Refuse(S["What's owed changed since this payment page was opened: it was paid or changed. Open the pay link again to see what's owed now."]);

                return;
            }

            if (await IsHeldByAnotherAsync(transaction, session.SessionId, cancellationToken))
            {
                context.Refuse(S["A payment for this was already started on another page and may still go through. Finish it there, or open the pay link again in a few minutes to see what's owed."]);

                return;
            }

            transactions.Add(transaction);
        }

        foreach (var transaction in transactions)
        {
            var hold = _holds.Take(GetHold(transaction), session.SessionId);

            transaction.Put(new TransactionSettlementHold
            {
                SessionId = hold.SessionId,
                HeldUtc = hold.HeldUtc,
            });

            await _transactions.UpdateAsync(transaction, cancellationToken: cancellationToken);
        }

        // Committed while the locks are held, so the next checkout of these transactions sees the hold.
        await _session.SaveChangesAsync(cancellationToken);
    }

    // Whether another checkout's payment for the transaction may still take the money. A succeeded payment the transaction
    // already recorded holds nothing, so the next part of a part-paid transaction is free to pay.
    private Task<bool> IsHeldByAnotherAsync(Transaction transaction, string sessionId, CancellationToken cancellationToken)
    {
        var applied = transaction.Events
            .Where(evt => evt.Type == TransactionEventType.PaymentRecorded && !string.IsNullOrEmpty(evt.PaymentAttemptId))
            .Select(evt => evt.PaymentAttemptId)
            .ToHashSet(StringComparer.Ordinal);

        return _holds.IsHeldByAnotherAsync(GetHold(transaction), sessionId, attempt => applied.Contains(attempt.ItemId), cancellationToken);
    }

    private static CheckoutPaymentHold GetHold(Transaction transaction)
        => transaction.TryGet<TransactionSettlementHold>(out var hold) && hold is not null
            ? new CheckoutPaymentHold { SessionId = hold.SessionId, HeldUtc = hold.HeldUtc }
            : null;
}
