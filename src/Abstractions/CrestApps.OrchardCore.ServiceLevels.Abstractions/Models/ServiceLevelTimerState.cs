namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// Where a service level timer is in its life. Whether it passed its target is a separate fact
/// (<see cref="ServiceLevelTimer.BreachedUtc"/>): a breached timer keeps running until the work is done.
/// </summary>
public enum ServiceLevelTimerState
{
    /// <summary>
    /// The timer is counting.
    /// </summary>
    Running = 0,

    /// <summary>
    /// The timer is stopped for now, such as while the work waits on the requester.
    /// </summary>
    Paused = 1,

    /// <summary>
    /// The work the timer measured was done.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The timer was dropped: its record was closed another way, or a new timer replaced it.
    /// </summary>
    Cancelled = 3,
}
