using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Everything the agent availability report says about one agent: how long they have waited, what they took today,
/// whether their session is live, what is stale, whether their recorded history agrees with their profile, and why
/// each of their queues and campaigns is or is not giving them work.
/// </summary>
internal sealed class AgentAvailabilityDiagnosis
{
    /// <summary>
    /// Gets the agent profile.
    /// </summary>
    public AgentProfile Agent { get; init; }

    /// <summary>
    /// Gets when the agent became idle, while they are Available: the time routing ranks longest-idle agents by.
    /// </summary>
    public DateTime? AvailableSinceUtc { get; init; }

    /// <summary>
    /// Gets when routing last gave the agent work.
    /// </summary>
    public DateTime? LastCallAssignedUtc { get; init; }

    /// <summary>
    /// Gets when the agent's last call ended.
    /// </summary>
    public DateTime? LastCallEndedUtc { get; init; }

    /// <summary>
    /// Gets how many calls the agent took today: the recorded changes into Busy since the start of the tenant's day.
    /// </summary>
    public int CallsToday { get; init; }

    /// <summary>
    /// Gets the longest stretch today the agent was ready for a call (Available or Reserved) without one, in seconds,
    /// counting a stretch still running up to now.
    /// </summary>
    public double LongestWaitSeconds { get; init; }

    /// <summary>
    /// Gets the queues the agent is signed in to, by name, campaigns' queues excluded.
    /// </summary>
    public IReadOnlyList<string> SignedInQueues { get; init; } = [];

    /// <summary>
    /// Gets the campaigns the agent is signed in to, by name.
    /// </summary>
    public IReadOnlyList<string> SignedInCampaigns { get; init; } = [];

    /// <summary>
    /// Gets whether the agent has a session at all.
    /// </summary>
    public bool HasSession { get; init; }

    /// <summary>
    /// Gets whether the agent's session is live: online, connected, and with a heartbeat within the timeout.
    /// </summary>
    public bool SessionLive { get; init; }

    /// <summary>
    /// Gets the agent's session's last heartbeat.
    /// </summary>
    public DateTime? LastHeartbeatUtc { get; init; }

    /// <summary>
    /// Gets whether the agent is signed in to any queue or campaign at all.
    /// </summary>
    public bool SignedInAnywhere { get; init; }

    /// <summary>
    /// Gets whether the live dashboard lists the agent for the person running the report.
    /// </summary>
    public bool OnDashboard { get; init; }

    /// <summary>
    /// Gets the stale-state flags raised on the agent.
    /// </summary>
    public IReadOnlyList<AgentAvailabilityFlag> Flags { get; init; } = [];

    /// <summary>
    /// Gets how the agent's profile and their recorded state history disagree.
    /// </summary>
    public AgentHistoryDisagreement HistoryDisagreement { get; init; }

    /// <summary>
    /// Gets the state the agent's last recorded change entered, when one is recorded.
    /// </summary>
    public AgentPresenceStatus? RecordedState { get; init; }

    /// <summary>
    /// Gets when the agent's last recorded change took effect.
    /// </summary>
    public DateTime? RecordedSinceUtc { get; init; }

    /// <summary>
    /// Gets whether the agent can be given work from each of their queues and campaigns.
    /// </summary>
    public IReadOnlyList<AgentQueueEligibility> Queues { get; init; } = [];
}
