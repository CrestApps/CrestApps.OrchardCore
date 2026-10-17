namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How one pacing cycle of a campaign queue ended.
/// </summary>
/// <remarks>
/// <see cref="Placed"/>, <see cref="NothingDue"/>, <see cref="NoAgents"/> and <see cref="HeldBack"/> are cycles that ran
/// to the end: they are what the dialer's heartbeat counts. The others are cycles that did not get to decide anything.
/// </remarks>
public enum DialerPacingCycleOutcome
{
    /// <summary>
    /// The cycle placed one or more calls and committed them.
    /// </summary>
    Placed = 0,

    /// <summary>
    /// The cycle found nothing it may dial now: no waiting record, or none due yet (scheduled for later, waiting out its
    /// retry delay, out of attempts) or the campaign's business hours are closed.
    /// </summary>
    NothingDue = 1,

    /// <summary>
    /// The cycle found work it may dial, but routing found no agent to take it.
    /// </summary>
    NoAgents = 2,

    /// <summary>
    /// The cycle found work and placed nothing because the dialer chose not to: the abandonment policy, the compliance
    /// gate, a profile that is turned off, or calls already in flight that cover the free agents.
    /// </summary>
    HeldBack = 3,

    /// <summary>
    /// The cycle did not run because another cycle, on this node or another, held the queue's pacing lock.
    /// </summary>
    LockNotAcquired = 4,

    /// <summary>
    /// The cycle, or the minute backstop that arms it, ran out of its time budget; nothing it staged was committed.
    /// </summary>
    TimeBudgetExceeded = 5,

    /// <summary>
    /// The cycle threw; nothing it staged was committed.
    /// </summary>
    Failed = 6,

    /// <summary>
    /// The cycle lost a write race with another transition; nothing it staged was committed and the queue is paced again
    /// shortly.
    /// </summary>
    WriteConflict = 7,
}
