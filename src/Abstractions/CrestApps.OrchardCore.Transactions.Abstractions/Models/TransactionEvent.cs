namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A single audit entry on a <see cref="Transaction"/> timeline. Events record the human-visible history of
/// a transaction (status changes, recorded payments, sent reminders, and manager notes) so both the owner
/// and administrators can see exactly what happened and when.
/// </summary>
public sealed class TransactionEvent
{
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
    /// Gets or sets the identifier of the user that caused the event, when applicable.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user that caused the event, when applicable.
    /// </summary>
    public string ActorName { get; set; }
}
