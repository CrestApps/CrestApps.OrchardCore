using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// One queue's intervals over one UTC day. Derived data: it can be deleted at any time and computed again from the
/// interactions.
/// </summary>
public sealed class QueueIntervalStatistics : CatalogItem
{
    /// <summary>
    /// Gets or sets the queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the intervals that had work, in order.
    /// </summary>
    public List<QueueIntervalStatistic> Intervals { get; set; } = [];
}
