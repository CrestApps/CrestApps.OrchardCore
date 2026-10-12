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
    /// Returns <see langword="true"/> when it makes sense to chase the owner for payment.
    /// </summary>
    /// <param name="transaction">The transaction to evaluate.</param>
    public static bool CanSendReminder(Transaction transaction)
        => transaction is not null &&
            IsCollectable(transaction.Status) &&
            transaction.OutstandingAmount > 0m;
}
