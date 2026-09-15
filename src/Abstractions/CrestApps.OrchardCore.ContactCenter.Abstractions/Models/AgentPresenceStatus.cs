namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the live availability state of a Contact Center agent.
/// </summary>
public enum AgentPresenceStatus
{
    /// <summary>
    /// The agent is signed out and cannot receive work.
    /// </summary>
    Offline,

    /// <summary>
    /// The agent is signed in and available to receive work.
    /// </summary>
    Available,

    /// <summary>
    /// The agent has been reserved for an offer but has not yet accepted it.
    /// </summary>
    Reserved,

    /// <summary>
    /// The agent is actively working an interaction.
    /// </summary>
    Busy,

    /// <summary>
    /// The agent is completing post-interaction wrap-up work.
    /// </summary>
    WrapUp,

    /// <summary>
    /// The agent is signed in but temporarily not ready for work.
    /// </summary>
    Break,

    /// <summary>
    /// The agent requested a break that will be granted when no assignment is in progress.
    /// </summary>
    RequestBreak,

    /// <summary>
    /// The agent is signed in but away from the desk.
    /// </summary>
    Away,

    /// <summary>
    /// The agent is signed in but should not receive work.
    /// </summary>
    DoNotDisturb,

    /// <summary>
    /// The agent is unavailable because they are in a meeting.
    /// </summary>
    Meeting,

    /// <summary>
    /// The agent is unavailable because they are in training.
    /// </summary>
    Training,

    /// <summary>
    /// The agent is unavailable outside staffed hours.
    /// </summary>
    AfterHoursUnavailable,
}
