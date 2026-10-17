using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Decides what a paced campaign queue's dialer is doing from when its work started waiting and its last completed
/// pacing cycle.
/// </summary>
/// <remarks>
/// The heartbeat is the last cycle that ran to the end and decided something, whatever it decided. The minute dialer
/// pacing task paces every campaign queue with waiting work, so while the dialer is healthy a cycle completes at least
/// once a minute even when it finds no agent or nothing due. Work that has waited longer than the stall threshold since
/// the later of that cycle and its own arrival therefore means pacing has stopped, not that there is nothing to do.
/// </remarks>
internal static class DialerPacingHealthEvaluator
{
    /// <summary>
    /// Decides a queue's state.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="stalledAfter">How long work may wait without a completed cycle before the queue is stalled.</param>
    /// <param name="waitingSinceUtc">When the queue's longest-waiting record was queued.</param>
    /// <param name="lastCompletedCycle">The queue's last cycle that ran to the end, or <see langword="null"/>.</param>
    /// <returns>The state, and how long work has waited without a completed cycle.</returns>
    public static (DialerPacingHealthState State, TimeSpan TimeWithoutCompletedCycle) Evaluate(
        DateTime nowUtc,
        TimeSpan stalledAfter,
        DateTime waitingSinceUtc,
        DialerPacingCycle lastCompletedCycle)
    {
        var heartbeatUtc = lastCompletedCycle is not null && lastCompletedCycle.CompletedUtc > waitingSinceUtc
            ? lastCompletedCycle.CompletedUtc
            : waitingSinceUtc;

        var silence = nowUtc > heartbeatUtc
            ? nowUtc - heartbeatUtc
            : TimeSpan.Zero;

        if (silence > stalledAfter)
        {
            return (DialerPacingHealthState.Stalled, silence);
        }

        var state = lastCompletedCycle?.Outcome switch
        {
            DialerPacingCycleOutcome.Placed => DialerPacingHealthState.Dialing,
            DialerPacingCycleOutcome.NoAgents => DialerPacingHealthState.Idle,
            DialerPacingCycleOutcome.HeldBack => DialerPacingHealthState.HeldBack,
            DialerPacingCycleOutcome.NothingDue => DialerPacingHealthState.NothingToCall,
            _ => DialerPacingHealthState.Starting,
        };

        return (state, silence);
    }

    /// <summary>
    /// Ranks a state by how much it needs someone's attention, so the most serious one is shown first.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>A larger number for a more serious state.</returns>
    public static int Severity(DialerPacingHealthState state)
        => state switch
        {
            DialerPacingHealthState.Stalled => 5,
            DialerPacingHealthState.Idle => 4,
            DialerPacingHealthState.HeldBack => 3,
            DialerPacingHealthState.Starting => 2,
            DialerPacingHealthState.Dialing => 1,
            _ => 0,
        };
}
