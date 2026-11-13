namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A single audit entry on a <see cref="Transaction"/> timeline. Events record the human-visible history of
/// a transaction (status changes, recorded payments, sent reminders, and manager notes) so both the owner
/// and administrators can see exactly what happened and when.
/// </summary>
public sealed class TransactionEvent
{
    /// <summary>
    /// Gets or sets the identifier of the event, unique within its transaction. A recorded payment is addressed
    /// by it, for example to print the receipt for that one payment. Events recorded before identifiers existed
    /// have none.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the event occurred.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the kind of event.
    /// </summary>
    public TransactionEventType Type { get; set; }

    /// <summary>
    /// Gets or sets the human-readable message describing the event.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the reason given for the event, as entered, when the event records a cancellation or an adjustment.
    /// Unlike <see cref="Message"/>, it holds only what the person or process gave as the reason, so it can be passed on
    /// to <see cref="Services.ITransactionEventHandler.CanceledAsync"/> and printed on the invoice.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets why the amount owed was lowered, when the event is an <see cref="TransactionEventType.Adjusted"/>
    /// event. Its <see cref="Amount"/> is the amount taken off.
    /// </summary>
    public TransactionAdjustmentKind? AdjustmentKind { get; set; }

    /// <summary>
    /// Gets or sets the reference of a payment recorded by hand, such as a check number or a bank transfer reference,
    /// when one was given.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that caused the event, when applicable.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user that caused the event, when applicable.
    /// </summary>
    public string ActorName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the confirmed payment attempt this event recorded, when the event is
    /// a recorded payment. It makes settlement idempotent: an attempt already present on the timeline is
    /// never applied to the balance again, even across separate checkout sessions or a replayed webhook.
    /// </summary>
    public string PaymentAttemptId { get; set; }

    /// <summary>
    /// Gets or sets the amount the event moved, in the transaction's currency, when the event is a recorded
    /// payment.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets how a recorded payment was made, using one of the
    /// <see cref="TransactionsConstants.SettlementMethods"/> values.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets how money received outside the site was paid, as the short key the person who recorded it chose,
    /// such as <c>cash</c>, <c>check</c> or <c>bank-transfer</c>, when the event is a payment recorded by hand.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets when the money of a payment recorded by hand was received, in UTC, when that was before the payment
    /// was recorded. <see cref="CreatedUtc"/> stays the time it was recorded.
    /// </summary>
    public DateTime? ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the receipt number issued for a recorded payment, for example <c>R-1001</c>. It is issued once,
    /// when the receipt is first sent, and every later copy of that receipt shows the same number.
    /// </summary>
    public string ReceiptNumber { get; set; }

    /// <summary>
    /// Gets or sets the kind of document the event was recorded for, such as <c>CreditNote</c>, when a document stands
    /// behind it: a credit note behind an adjustment, for example.
    /// </summary>
    public string DocumentKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the document named by <see cref="DocumentKind"/>.
    /// </summary>
    public string DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the number of the document named by <see cref="DocumentKind"/>, such as <c>CN-1001</c>, as it is
    /// shown to people.
    /// </summary>
    public string DocumentNumber { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the earlier event this event takes back, when it does. A payment or an adjustment
    /// recorded by mistake is never edited or removed: an event that reverses it is added, so the timeline keeps
    /// everything that happened.
    /// </summary>
    public string ReversesEventId { get; set; }
}
