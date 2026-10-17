using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// The time an agent spent on one day in one state, with one reason, signed in to one set of queues and campaigns.
/// </summary>
public sealed class AgentDailyTimeBucket
{
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the reason the state was entered with, when it had one.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent was signed in to.
    /// </summary>
    public List<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the agent was signed in to.
    /// </summary>
    public List<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the time on the day, in ticks.
    /// </summary>
    public long Ticks { get; set; }

    /// <summary>
    /// Gets or sets how many intervals in this state started on the day.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets whether the bucket is the state the agent was already in when the day started. Its time belongs to
    /// the day, but the interval is counted on the day it started.
    /// </summary>
    public bool Continued { get; set; }
}
