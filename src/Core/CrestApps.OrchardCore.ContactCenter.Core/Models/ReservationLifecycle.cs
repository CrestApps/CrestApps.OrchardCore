using System.Collections.Frozen;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Declares which reservation status changes the domain admits.
/// <para>
/// A reservation is a short-lived lock, and the whole value of the lock is that it resolves exactly once. The
/// races it exists to settle are real: an agent accepts at the same moment the timer expires, or a supervisor
/// cancels while the agent is rejecting. Letting an already-resolved reservation resolve again is what allows
/// the same activity to be handed to two agents.
/// </para>
/// </summary>
public static class ReservationLifecycle
{
    private static readonly FrozenDictionary<ReservationStatus, FrozenSet<ReservationStatus>> _transitions =
        new Dictionary<ReservationStatus, FrozenSet<ReservationStatus>>
        {
            [ReservationStatus.Pending] = FrozenSet.ToFrozenSet(
            [
                ReservationStatus.Accepted,
                ReservationStatus.Rejected,
                ReservationStatus.Expired,
                ReservationStatus.Canceled,
            ]),

            // An accepted reservation can still be cancelled: the assignment it produced may be released before
            // the agent starts work. Every other outcome is final.
            [ReservationStatus.Accepted] = FrozenSet.ToFrozenSet(
            [
                ReservationStatus.Canceled,
            ]),
            [ReservationStatus.Rejected] = FrozenSet<ReservationStatus>.Empty,
            [ReservationStatus.Expired] = FrozenSet<ReservationStatus>.Empty,
            [ReservationStatus.Canceled] = FrozenSet<ReservationStatus>.Empty,
        }.ToFrozenDictionary();

    /// <summary>
    /// Determines whether a reservation in one status may move to another.
    /// </summary>
    /// <param name="from">The status the reservation is in.</param>
    /// <param name="to">The status the reservation would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(ReservationStatus from, ReservationStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return _transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Determines whether a reservation status is resolved, meaning the lock is no longer held.
    /// </summary>
    /// <param name="status">The status to inspect.</param>
    /// <returns><see langword="true"/> when the reservation is resolved; otherwise <see langword="false"/>.</returns>
    public static bool IsResolved(ReservationStatus status)
        => status != ReservationStatus.Pending;
}
