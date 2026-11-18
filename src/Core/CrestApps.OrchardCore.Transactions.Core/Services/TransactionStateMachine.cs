using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Decides which management operations a <see cref="Transaction"/> will accept in its current state.
/// </summary>
/// <remarks>
/// The management screens each load a transaction, mutate it, and save it. Without a shared notion of which
/// transitions are legal, every screen re-implements the rule, and the ones that forget let an operator record
/// a payment against a canceled obligation or mark an already-refunded one paid — corrupting a ledger whose
/// whole purpose is to answer "what is still owed". Keeping the rules in one place also means a new management
/// surface inherits them instead of inventing its own.
/// </remarks>
public static class TransactionStateMachine
{
    /// <summary>
    /// Returns <see langword="true"/> when the transaction is still collectable, meaning money may be recorded
    /// against it and it may be settled or canceled. A transaction that already reached a terminal state
    /// (paid, canceled, refunded, or abandoned) is not collectable.
    /// </summary>
    /// <param name="status">The current status.</param>
    public static bool IsCollectable(TransactionStatus status)
        => status is TransactionStatus.Pending
            or TransactionStatus.Outstanding
            or TransactionStatus.PartiallyPaid
            or TransactionStatus.Failed;

    /// <summary>
    /// Returns <see langword="true"/> when a payment may be recorded against the transaction.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanRecordPayment(Transaction transaction)
        => transaction is not null &&
            IsCollectable(transaction.Status) &&
            transaction.OutstandingAmount > 0m;

    /// <summary>
    /// Returns <see langword="true"/> when the transaction may be settled in full.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanMarkPaid(Transaction transaction)
        => transaction is not null && IsCollectable(transaction.Status);

    /// <summary>
    /// Returns <see langword="true"/> when the transaction may be canceled. An obligation that was already paid
    /// is not canceled but refunded, which is a money movement rather than a state correction.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanCancel(Transaction transaction)
        => transaction is not null && IsCollectable(transaction.Status);

    /// <summary>
    /// Returns <see langword="true"/> when what is owed may be lowered by an adjustment (a discount, waiver, credit or
    /// write-off). Only a collectable transaction that still owes money can be adjusted.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanAdjust(Transaction transaction)
        => transaction is not null &&
            IsCollectable(transaction.Status) &&
            transaction.OutstandingAmount > 0m;

    /// <summary>
    /// Works out what an online payment of a requested amount pays: the whole of what is owed unless the transaction
    /// accepts part payments, never more than what is owed, and never less than the smallest part payment the
    /// transaction accepts (what is owed can always be paid in full, even when it is less than that).
    /// </summary>
    /// <param name="transaction">The transaction to pay.</param>
    /// <param name="requested">The amount the payer asked to pay, or <see langword="null"/> to pay everything.</param>
    /// <param name="amount">The amount to charge, at the currency's precision.</param>
    /// <returns><see langword="true"/> when the requested amount is accepted; <see langword="false"/> when it is below
    /// the smallest part payment, in which case <paramref name="amount"/> is that smallest payment.</returns>
    public static bool TryGetPaymentAmount(Transaction transaction, decimal? requested, out decimal amount)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var outstanding = CurrencyScale.Round(transaction.OutstandingAmount, transaction.Currency);

        amount = outstanding;

        if (!transaction.AllowPartialPayment || requested is not decimal asked)
        {
            return true;
        }

        var rounded = CurrencyScale.Round(asked, transaction.Currency);

        if (rounded >= outstanding)
        {
            return true;
        }

        var minimum = Math.Min(outstanding, CurrencyScale.Round(Math.Max(0m, transaction.MinimumPayment ?? 0m), transaction.Currency));

        if (rounded <= 0m || rounded < minimum)
        {
            amount = minimum > 0m ? minimum : outstanding;

            return false;
        }

        amount = rounded;

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a timeline entry may be taken back: a payment recorded by hand or an
    /// adjustment, that was not taken back already, on a transaction that was not canceled or refunded.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    /// <param name="entry">The timeline entry to take back.</param>
    public static bool CanReverse(Transaction transaction, TransactionEvent entry)
    {
        if (transaction is null || entry is null || string.IsNullOrEmpty(entry.Id) || entry.Amount is not > 0m)
        {
            return false;
        }

        if (transaction.Status is TransactionStatus.Canceled or TransactionStatus.Refunded or TransactionStatus.Abandoned)
        {
            return false;
        }

        // Money taken online went through a gateway: it is given back with a refund, never by editing the ledger.
        var reversible = entry.Type == TransactionEventType.Adjusted ||
            (entry.Type == TransactionEventType.PaymentRecorded && string.IsNullOrEmpty(entry.PaymentAttemptId));

        return reversible &&
            !transaction.Events.Any(other => string.Equals(other.ReversesEventId, entry.Id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the invoice may be sent to the owner: the transaction still owes money and
    /// the owner pays it themselves rather than it being charged automatically.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanSendInvoice(Transaction transaction)
        => CanSendReminder(transaction) && transaction.AutoCollection is null;

    /// <summary>
    /// Returns <see langword="true"/> when it makes sense to chase the owner for payment.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanSendReminder(Transaction transaction)
        => transaction is not null &&
            IsCollectable(transaction.Status) &&
            transaction.OutstandingAmount > 0m;
}
