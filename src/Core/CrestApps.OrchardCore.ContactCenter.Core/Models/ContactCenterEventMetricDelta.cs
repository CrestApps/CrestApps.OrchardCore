using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an unrolled contribution to a daily event count. Counting by reading the daily total, adding to
/// it and writing it back makes that one row a serialization point: every event of the same type on the same
/// day contends for it, and under optimistic concurrency the loser either fails or overwrites a count it never
/// saw. A contribution is instead appended and never updated, so concurrent writers do not meet, and the
/// contributions are folded into the daily total afterwards by a single roller.
/// </summary>
public sealed class ContactCenterEventMetricDelta : CatalogItem
{
    /// <summary>
    /// Gets or sets the day the contribution counts toward, formatted as <c>yyyy-MM-dd</c> (UTC).
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
    /// Gets or sets the number of events this contribution represents.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the contribution was appended.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
