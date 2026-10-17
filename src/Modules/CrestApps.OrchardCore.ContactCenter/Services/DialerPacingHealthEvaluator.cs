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

        // A cycle that completed before the work now waiting arrived decided about other work: it says nothing about this
        // work, so it can neither say that nothing is due nor that calls are being placed.
        if (lastCompletedCycle is null || lastCompletedCycle.CompletedUtc <= waitingSinceUtc)
        {
            return (DialerPacingHealthState.Starting, silence);
        }

        var state = lastCompletedCycle.Outcome switch
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
    /// Decides whether a queue's pacing lock has stayed out of reach: its latest cycle could not take the lock, and no cycle
    /// has run with it for longer than the threshold.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="threshold">How long every cycle may miss the lock before that is a fault.</param>
    /// <param name="lastCycle">The queue's latest cycle, however it ended, or <see langword="null"/>.</param>
    /// <param name="lastCycleNotLockMiss">
    /// The queue's latest cycle that ended any other way than <see cref="DialerPacingCycleOutcome.LockNotAcquired"/>, or
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Since when the lock has not been acquired, or <see langword="null"/> when the queue is not starved of its lock.
    /// </returns>
    public static DateTime? FindLockNotAcquiredSince(
        DateTime nowUtc,
        TimeSpan threshold,
        DialerPacingCycle lastCycle,
        DialerPacingCycle lastCycleNotLockMiss)
    {
        if (lastCycle?.Outcome != DialerPacingCycleOutcome.LockNotAcquired)
        {
            return null;
        }

        // Without a cycle that ran, the misses folded into the latest record are the oldest evidence there is: the record
        // keeps when the first of them started.
        var sinceUtc = lastCycleNotLockMiss is not null && lastCycleNotLockMiss.CompletedUtc < lastCycle.CompletedUtc
            ? lastCycleNotLockMiss.CompletedUtc
            : lastCycle.FirstStartedUtc == default ? lastCycle.StartedUtc : lastCycle.FirstStartedUtc;

        return nowUtc - sinceUtc > threshold
            ? sinceUtc
            : null;
    }

    /// <summary>
    /// Ranks a state by how much it needs someone's attention, so the most serious one is shown first.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>A larger number for a more serious state.</returns>
    public static int Severity(DialerPacingHealthState state)
        => state switch
        {
            DialerPacingHealthState.LockNotAcquired => 6,
            DialerPacingHealthState.Stalled => 5,
            DialerPacingHealthState.Idle => 4,
            DialerPacingHealthState.HeldBack => 3,
            DialerPacingHealthState.Starting => 2,
            DialerPacingHealthState.Dialing => 1,
            _ => 0,
        };
}
