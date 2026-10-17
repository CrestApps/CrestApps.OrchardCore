namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What a paced campaign queue's dialer is doing, judged from its waiting work and its pacing cycles.
/// </summary>
/// <remarks>
/// <see cref="Stalled"/>, <see cref="Idle"/> and <see cref="NothingToCall"/> are told apart on purpose: a dialer whose
/// cycles stopped, one whose cycles run but find no agent, and one whose cycles run but find nothing due all place no
/// calls, and only the first is a fault.
/// </remarks>
public enum DialerPacingHealthState
{
    /// <summary>
    /// Nothing is due: no paced campaign has work waiting, or the queue's cycles find nothing they may dial now.
    /// </summary>
    NothingToCall = 0,

    /// <summary>
    /// Work arrived recently and the queue's first pacing cycle has not completed yet.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// The queue's latest completed cycle placed calls.
    /// </summary>
    Dialing = 2,

    /// <summary>
    /// The queue's cycles complete, but find no agent to take the work that is due.
    /// </summary>
    Idle = 3,

    /// <summary>
    /// The queue's cycles complete, but the dialer holds back: the abandonment policy, the compliance gate or calls
    /// already in flight.
    /// </summary>
    HeldBack = 4,

    /// <summary>
    /// Work is waiting but no pacing cycle of the queue has completed for longer than the stall threshold.
    /// </summary>
    Stalled = 5,
}
