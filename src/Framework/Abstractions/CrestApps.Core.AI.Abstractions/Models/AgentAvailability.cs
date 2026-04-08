namespace CrestApps.Core.AI.Models;

/// <summary>
/// Defines the availability mode for an AI agent.
/// </summary>
public enum AgentAvailability
{
    /// <summary>
    /// The agent is included only when matched by semantic or keyword scoring.
    /// This is the default mode and minimizes token usage.
    /// </summary>
    OnDemand,

    /// <summary>
    /// The agent is always included in every completion request automatically.
    /// This mode increases token usage but ensures the agent is always available.
    /// </summary>
    AlwaysAvailable,
}
