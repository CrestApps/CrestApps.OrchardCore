using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to enforce and query per-handler event idempotency markers.
/// </summary>
public sealed class ContactCenterProcessedEventIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the stable, versioned identifier of the handler that processed the event.
    /// </summary>
    public string HandlerId { get; set; }

    /// <summary>
    /// Gets or sets the durable identifier of the event that was processed.
    /// </summary>
    public string EventId { get; set; }
}
