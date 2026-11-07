using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.StateMachines;

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
    private static readonly TransitionTable<ReservationStatus> _transitions = new(
        new Dictionary<ReservationStatus, ReservationStatus[]>
        {
            [ReservationStatus.Pending] =
            [
                ReservationStatus.Accepted,
                ReservationStatus.Rejected,
                ReservationStatus.Expired,
                ReservationStatus.Canceled,
            ],

            // An accepted reservation can still be cancelled: the assignment it produced may be released before
            // the agent starts work. Every other outcome is final.
            [ReservationStatus.Accepted] =
            [
                ReservationStatus.Canceled,
            ],
            [ReservationStatus.Rejected] = [],
            [ReservationStatus.Expired] = [],
            [ReservationStatus.Canceled] = [],
        },
        allowStaying: true);

    /// <summary>
    /// Determines whether a reservation in one status may move to another.
    /// </summary>
    /// <param name="from">The status the reservation is in.</param>
    /// <param name="to">The status the reservation would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(ReservationStatus from, ReservationStatus to)
        => _transitions.CanMove(from, to);

    /// <summary>
    /// Determines whether a reservation status is resolved, meaning the lock is no longer held.
    /// </summary>
    /// <param name="status">The status to inspect.</param>
    /// <returns><see langword="true"/> when the reservation is resolved; otherwise <see langword="false"/>.</returns>
    public static bool IsResolved(ReservationStatus status)
        => status != ReservationStatus.Pending;
}
