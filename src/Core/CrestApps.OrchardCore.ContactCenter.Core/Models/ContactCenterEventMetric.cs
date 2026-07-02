using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a daily count of a Contact Center domain event type. It is the projection that powers
/// historical volume reporting and is rebuildable from the durable event log.
/// </summary>
public sealed class ContactCenterEventMetric : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the day the events were counted for, formatted as <c>yyyy-MM-dd</c> (UTC).
    /// </summary>
    public string DateKey { get; set; }

    /// <summary>
    /// Gets or sets the UTC date (midnight) the events were counted for, used for range queries.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the domain event type being counted.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the number of events of this type on this day.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the metric was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the metric was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
