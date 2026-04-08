namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata for AI profiles with <see cref="AIProfileType.Agent"/> type.
/// Stored in the profile's Properties via Put/As pattern.
/// </summary>
public sealed class AgentMetadata
{
    /// <summary>
    /// Gets or sets the availability mode for this agent.
    /// <see cref="AgentAvailability.OnDemand"/> agents are included only when matched
    /// by semantic or keyword scoring. <see cref="AgentAvailability.AlwaysAvailable"/> agents
    /// are automatically included in every completion request and are not shown in the
    /// user-selectable agent list.
    /// </summary>
    public AgentAvailability Availability { get; set; }
}
