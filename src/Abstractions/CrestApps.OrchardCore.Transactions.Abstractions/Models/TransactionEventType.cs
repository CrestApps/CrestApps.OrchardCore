namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The kind of audit event recorded on a <see cref="Transaction"/> timeline.
/// </summary>
public enum TransactionEventType
{
    /// <summary>
    /// The transaction was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The transaction status changed.
    /// </summary>
    StatusChanged = 1,

    /// <summary>
    /// A payment was recorded against the transaction.
    /// </summary>
    PaymentRecorded = 2,

    /// <summary>
    /// A payment reminder was sent to the transaction owner.
    /// </summary>
    ReminderSent = 3,

    /// <summary>
    /// The transaction was canceled.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// A free-form note was added by a manager.
    /// </summary>
    Note = 5,
}
