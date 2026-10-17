namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// One agent's time on one day of an <see cref="AgentDailyTimeSummary"/>.
/// </summary>
public sealed class AgentDailyTime
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets whether the log holds a state change of the agent on the day, rather than only the state the agent
    /// was already in when it started.
    /// </summary>
    public bool HadEvents { get; set; }

    /// <summary>
    /// Gets or sets the agent's time on the day, grouped by state, reason, queues and campaigns.
    /// </summary>
    public List<AgentDailyTimeBucket> Buckets { get; set; } = [];
}
