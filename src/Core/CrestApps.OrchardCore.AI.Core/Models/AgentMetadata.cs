namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Metadata for AI profiles with <see cref="Models.AIProfileType.Agent"/> type.
/// Stored in the profile's Properties via Put/As pattern.
/// </summary>
public sealed class AgentMetadata
{
    /// <summary>
    /// Gets or sets whether this agent is a system agent.
    /// System agents are automatically included by the orchestrator
    /// based on context and are not shown in the user-selectable agent list.
    /// </summary>
    public bool IsSystemAgent { get; set; }
}
