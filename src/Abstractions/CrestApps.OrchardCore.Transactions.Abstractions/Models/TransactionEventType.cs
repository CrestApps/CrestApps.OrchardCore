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

    /// <summary>
    /// Money paid against the transaction was given back.
    /// </summary>
    Refunded = 6,

    /// <summary>
    /// What is owed was lowered without money being received: a discount, waiver, credit or write-off.
    /// </summary>
    Adjusted = 7,

    /// <summary>
    /// The invoice was sent to the owner.
    /// </summary>
    InvoiceSent = 8,

    /// <summary>
    /// A charge of a saved payment method, made without the owner present, was not taken: declined, refused because the
    /// cardholder must approve it, or not started at the provider.
    /// </summary>
    PaymentFailed = 9,
}
