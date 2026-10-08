using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query unrolled daily event count contributions.
/// </summary>
public sealed class ContactCenterEventMetricDeltaIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the day the contribution counts toward, formatted as <c>yyyy-MM-dd</c>.
    /// </summary>
    public string DateKey { get; set; }

    /// <summary>
    /// Gets or sets the UTC date (midnight) the contribution counts toward.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the domain event type being counted.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the number of events this contribution represents. It is carried on the index so the
    /// contributions waiting to be folded can be totalled without joining to the documents that hold them.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the contribution was appended.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
