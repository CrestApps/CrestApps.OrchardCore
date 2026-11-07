using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a durable outbox entry that tracks an event until every registered handler has completed.
/// The interaction event itself remains the immutable audit record; this message carries the mutable
/// delivery state so application restarts and transient failures cannot silently drop a domain event.
/// </summary>
/// <remarks>
/// It is an item of the shared leased work queue. Its stored <see cref="Status"/> keeps its own enum, whose values match
/// <see cref="LeasedWorkItemStatus"/>, so existing messages read unchanged.
/// </remarks>
public sealed class ContactCenterOutboxMessage : CatalogItem, IModifiedUtcAwareModel, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the identifier of the <see cref="InteractionEvent"/> this message retries.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the canonical event type name, retained for diagnostics and filtering.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the dispatch state of the message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; }

    /// <inheritdoc/>
    LeasedWorkItemStatus ILeasedWorkItem.Status
    {
        get => (LeasedWorkItemStatus)Status;
        set => Status = (OutboxMessageStatus)value;
    }

    /// <summary>
    /// Gets or sets the opaque token identifying the worker that owns the current claim.
    /// </summary>
    public string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing fence token assigned to the current claim.
    /// </summary>
    public long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the number of dispatch attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next dispatch attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the message from the last failed dispatch attempt.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the stable handler identifiers required to complete this message.
    /// </summary>
    public IList<string> ExpectedHandlerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the handler type names that have already processed the event successfully.
    /// </summary>
    public IList<string> CompletedHandlerTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the message was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
