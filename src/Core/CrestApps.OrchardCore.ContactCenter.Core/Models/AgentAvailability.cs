namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the computed routing availability of an agent at a point in time.
/// </summary>
public sealed class AgentAvailability
{
    /// <summary>
    /// Gets or sets the agent profile.
    /// </summary>
    public AgentProfile Agent { get; set; }

    /// <summary>
    /// Gets or sets the last recorded session heartbeat in UTC.
    /// </summary>
    public DateTime LastHeartbeatUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of active interactions consuming the agent's capacity.
    /// </summary>
    public int ActiveInteractionCount { get; set; }

    /// <summary>
    /// Gets the remaining interaction capacity.
    /// </summary>
    public int RemainingCapacity => Math.Max(0, Math.Max(1, Agent.MaxConcurrentInteractions) - ActiveInteractionCount);
}
