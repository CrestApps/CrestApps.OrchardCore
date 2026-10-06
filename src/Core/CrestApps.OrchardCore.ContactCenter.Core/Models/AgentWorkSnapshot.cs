namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a busy agent is doing now, for predicting when they will be free to take a call.
/// </summary>
public sealed class AgentWorkSnapshot
{
    /// <summary>
    /// Gets or sets the identifier of the agent.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets what the agent is doing.
    /// </summary>
    public AgentWorkPhase Phase { get; set; }

    /// <summary>
    /// Gets or sets how long the agent has been in <see cref="Phase"/>.
    /// </summary>
    public TimeSpan Elapsed { get; set; }
}
