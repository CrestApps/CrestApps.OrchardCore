namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the way a supervisor engages a live call.
/// </summary>
public enum MonitorMode
{
    /// <summary>
    /// The supervisor listens silently; neither party hears the supervisor.
    /// </summary>
    Monitor,

    /// <summary>
    /// The supervisor speaks only to the agent; the customer does not hear the supervisor.
    /// </summary>
    Whisper,

    /// <summary>
    /// The supervisor joins the call so all parties hear the supervisor.
    /// </summary>
    Barge,

    /// <summary>
    /// The supervisor takes the call over from the agent.
    /// </summary>
    TakeOver,
}
