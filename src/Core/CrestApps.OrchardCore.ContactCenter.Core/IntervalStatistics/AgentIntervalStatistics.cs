using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// One agent's intervals over one UTC day. Derived data: it can be deleted at any time and computed again from the
/// agent state log and the interactions.
/// </summary>
public sealed class AgentIntervalStatistics : CatalogItem
{
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

    /// <summary>
    /// Gets or sets the intervals the agent was signed in or handled work, in order.
    /// </summary>
    public List<AgentIntervalStatistic> Intervals { get; set; } = [];
}
