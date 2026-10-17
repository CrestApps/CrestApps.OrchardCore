using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Records how each dialer pacing cycle ended.
/// </summary>
/// <remarks>
/// A record is always written on a scope of its own, never with the cycle's session, so it is kept whether the cycle's
/// transaction committed, was rolled back or was abandoned. A failure to write it is logged rather than thrown into the
/// pacing that asked for it.
/// </remarks>
internal interface IDialerPacingCycleRecorder
{
    /// <summary>
    /// Records a cycle that has just ended, stamping when it completed. Inside a shell scope the record is written once
    /// that scope has ended normally; outside one it is written straight away.
    /// </summary>
    /// <param name="cycle">The cycle, with its queue, profile, outcome and start time set.</param>
    Task RecordAsync(DialerPacingCycle cycle);

    /// <summary>
    /// Records a cycle that has just ended straight away, stamping when it completed. Used when the scope the cycle ran on
    /// is about to fail, since what waits for a failed scope to end never runs.
    /// </summary>
    /// <param name="cycle">The cycle, with its queue, profile, outcome and start time set.</param>
    Task RecordNowAsync(DialerPacingCycle cycle);
}
