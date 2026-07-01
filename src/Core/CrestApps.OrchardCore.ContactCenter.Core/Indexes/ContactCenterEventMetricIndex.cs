using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query daily event metrics.
/// </summary>
public sealed class ContactCenterEventMetricIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the day the metric counts, formatted as <c>yyyy-MM-dd</c>.
    /// </summary>
    public string DateKey { get; set; }

    /// <summary>
    /// Gets or sets the UTC date (midnight) the metric counts, used for range queries.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the domain event type being counted.
    /// </summary>
    public string EventType { get; set; }
}
