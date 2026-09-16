using System.Collections.Frozen;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Declares which routing-owned assignment status changes the domain admits for a
/// <see cref="ContactCenterWorkState"/>.
/// <para>
/// Unlike the communication lifecycles, this one is genuinely cyclic: work is released and becomes available
/// again, and that is normal rather than exceptional. What it must refuse is a status appearing without the step
/// that produces it — work that is in progress without ever having been assigned has no agent, and routing will
/// treat it as busy while nobody is working it.
/// </para>
/// </summary>
public static class WorkAssignmentLifecycle
{
    private static readonly FrozenDictionary<ActivityAssignmentStatus, FrozenSet<ActivityAssignmentStatus>> _transitions =
        new Dictionary<ActivityAssignmentStatus, FrozenSet<ActivityAssignmentStatus>>
        {
            [ActivityAssignmentStatus.Unassigned] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Available,
                ActivityAssignmentStatus.Reserved,
                ActivityAssignmentStatus.Assigned,
                ActivityAssignmentStatus.Released,
            ]),
            [ActivityAssignmentStatus.Available] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Unassigned,
                ActivityAssignmentStatus.Reserved,
                ActivityAssignmentStatus.Assigned,
                ActivityAssignmentStatus.Released,
            ]),

            // A reservation that expires or is rejected puts the work back where routing can see it, so
            // Reserved reaches Available and Unassigned as well as Assigned.
            [ActivityAssignmentStatus.Reserved] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Unassigned,
                ActivityAssignmentStatus.Available,
                ActivityAssignmentStatus.Assigned,
                ActivityAssignmentStatus.Released,
            ]),
            [ActivityAssignmentStatus.Assigned] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Unassigned,
                ActivityAssignmentStatus.Available,
                ActivityAssignmentStatus.InProgress,
                ActivityAssignmentStatus.Released,
            ]),
            [ActivityAssignmentStatus.InProgress] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Unassigned,
                ActivityAssignmentStatus.Available,
                ActivityAssignmentStatus.Released,
            ]),

            // Released is not terminal. The same work is dialed again on a later cycle, and the healing service
            // returns abandoned work to the pool, so released work must be able to become available again.
            [ActivityAssignmentStatus.Released] = FrozenSet.ToFrozenSet(
            [
                ActivityAssignmentStatus.Unassigned,
                ActivityAssignmentStatus.Available,
                ActivityAssignmentStatus.Reserved,
                ActivityAssignmentStatus.Assigned,
            ]),
        }.ToFrozenDictionary();

    /// <summary>
    /// Determines whether work in one assignment status may move to another.
    /// </summary>
    /// <param name="from">The status the work is in.</param>
    /// <param name="to">The status the work would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(ActivityAssignmentStatus from, ActivityAssignmentStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return _transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }
}
