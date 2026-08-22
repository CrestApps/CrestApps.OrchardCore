namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Defines the liveness and after-call recovery policy for Contact Center agents.
/// </summary>
public sealed class AgentAvailabilityOptions
{
    /// <summary>
    /// Gets or sets the maximum age of a heartbeat that is considered live for routing.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Gets or sets the maximum time an agent may remain in after-call wrap-up before capacity is recovered.
    /// </summary>
    public TimeSpan MaximumWrapUpDuration { get; set; } = TimeSpan.FromMinutes(15);
}
