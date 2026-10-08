namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The part of handling a call a busy agent is in.
/// </summary>
public enum AgentWorkPhase
{
    /// <summary>
    /// The agent is on a call, and will wrap it up when it ends.
    /// </summary>
    Talking = 0,

    /// <summary>
    /// The call has ended and the agent is wrapping it up.
    /// </summary>
    WrappingUp = 1,
}
