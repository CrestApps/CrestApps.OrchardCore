using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a durable marker recording that one handler already applied one durable event. It is the
/// idempotency ledger that lets an at-least-once handler dedupe a replayed event by its stable event id
/// instead of relying on in-memory state.
/// </summary>
public sealed class ContactCenterProcessedEvent : CatalogItem
{
    /// <summary>
    /// Gets or sets the stable, versioned identifier of the handler that processed the event.
    /// </summary>
    public string HandlerId { get; set; }

    /// <summary>
    /// Gets or sets the durable identifier of the event that was processed.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the handler recorded the event as processed.
    /// </summary>
    public DateTime ProcessedUtc { get; set; }
}
