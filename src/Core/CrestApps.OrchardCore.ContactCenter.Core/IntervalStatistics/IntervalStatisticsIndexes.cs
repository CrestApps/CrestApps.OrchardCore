using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Indexes the computed days of the interval statistics.
/// </summary>
public sealed class IntervalStatisticsDayIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the version of the computation.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets whether the day is final.
    /// </summary>
    public bool Final { get; set; }

    /// <summary>
    /// Gets or sets how many interactions the day had when it was computed.
    /// </summary>
    public int InteractionCount { get; set; }

    /// <summary>
    /// Gets or sets when the day was computed.
    /// </summary>
    public DateTime ComputedUtc { get; set; }
}

/// <summary>
/// Indexes a queue's day of intervals by queue and day.
/// </summary>
public sealed class QueueIntervalStatisticsIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }
}

/// <summary>
/// Indexes an agent's day of intervals by agent, user and day.
/// </summary>
public sealed class AgentIntervalStatisticsIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent's user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }
}
