using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Reacts to the lifecycle of a <see cref="Transaction"/>: when it is created, when its status changes (with the
/// status it had before), when it is canceled, and when it becomes overdue. It is the one place for a module that keeps
/// its own state in step with a transaction, such as a registration that is unpaid, paid or refunded, to learn about
/// every change, however it happened: a payment, a refund, an administrator canceling it, an installment charge
/// failing, or the due date passing.
/// </summary>
/// <remarks>
/// Handlers run after the change is saved. A handler that fails is logged and does not undo the change or stop the
/// other handlers. Derive from <see cref="TransactionEventHandlerBase"/> to implement only the events you need.
/// </remarks>
public interface ITransactionEventHandler
{
    /// <summary>
    /// Called after a transaction was created.
    /// </summary>
    /// <param name="transaction">The transaction, as saved.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a save changed the status of a transaction. It is raised once per save that changes the status, and
    /// never for a save that leaves the status as it was.
    /// </summary>
    /// <param name="transaction">The transaction, as saved with its new status.</param>
    /// <param name="previous">The status the transaction had before the save.</param>
    /// <param name="current">The status the transaction has now.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a transaction was canceled, right after <see cref="StatusChangedAsync"/> reported the change to
    /// <see cref="TransactionStatus.Canceled"/>.
    /// </summary>
    /// <param name="transaction">The canceled transaction, as saved.</param>
    /// <param name="reason">The reason given for canceling it, or <see langword="null"/> when none was given.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called once per transaction, by the reminder sweep, the first time it finds the transaction unpaid after the day
    /// it was due. <see cref="Transaction.BecameOverdueUtc"/> records that it was raised.
    /// </summary>
    /// <param name="transaction">The overdue transaction, as saved.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
