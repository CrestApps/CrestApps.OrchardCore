namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Names how an agent's live profile and their recorded state history disagree. The recorded history is what the time
/// summary and the other workforce reports count from, so a disagreement explains why those reports and the live views
/// tell different stories about the same agent.
/// </summary>
[Flags]
internal enum AgentHistoryDisagreement
{
    /// <summary>
    /// The profile and the recorded history agree.
    /// </summary>
    None = 0,

    /// <summary>
    /// The last recorded change left the agent signed in and no sign-out followed, so the workforce reports count the
    /// agent in that state up to now, but the agent has no live session.
    /// </summary>
    CountedSignedInWithoutLiveSession = 1,

    /// <summary>
    /// The profile's state is not the state the last recorded change entered.
    /// </summary>
    StateDiffers = 2,

    /// <summary>
    /// No state change is recorded for the agent at all, although the profile says they are signed in.
    /// </summary>
    NoRecordedState = 4,
}
