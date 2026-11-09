using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Dispatch.Core;

/// <summary>
/// The moves a visit's status can make.
/// </summary>
public static class VisitStatusRules
{
    private static readonly TransitionTable<VisitStatus> _next = new(new Dictionary<VisitStatus, VisitStatus[]>
    {
        [VisitStatus.Unscheduled] = [VisitStatus.Canceled],
        [VisitStatus.Scheduled] = [VisitStatus.Dispatched, VisitStatus.EnRoute, VisitStatus.OnSite, VisitStatus.Canceled],
        [VisitStatus.Dispatched] = [VisitStatus.EnRoute, VisitStatus.OnSite, VisitStatus.Canceled],
        [VisitStatus.EnRoute] = [VisitStatus.OnSite, VisitStatus.Incomplete, VisitStatus.Canceled],
        [VisitStatus.OnSite] = [VisitStatus.Paused, VisitStatus.Completed, VisitStatus.Incomplete],
        [VisitStatus.Paused] = [VisitStatus.OnSite, VisitStatus.Completed, VisitStatus.Incomplete],
        [VisitStatus.Completed] = [],
        [VisitStatus.Incomplete] = [],
        [VisitStatus.Canceled] = [],
    });

    /// <summary>
    /// Gives the statuses a visit can move to with a status change.
    /// </summary>
    /// <param name="from">The status it is in.</param>
    /// <returns>The statuses.</returns>
    public static IReadOnlyList<VisitStatus> Next(VisitStatus from) => _next.Next(from);

    /// <summary>
    /// Tells whether a visit can move from one status to another with a status change.
    /// </summary>
    /// <param name="from">The status it is in.</param>
    /// <param name="to">The status it moves to.</param>
    /// <returns>Whether it can.</returns>
    public static bool CanMove(VisitStatus from, VisitStatus to) => _next.CanMove(from, to);

    /// <summary>
    /// Tells whether a visit is still to happen or happening.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether it is open.</returns>
    public static bool IsOpen(VisitStatus status) => status is not (VisitStatus.Completed or VisitStatus.Incomplete or VisitStatus.Canceled);

    /// <summary>
    /// Tells whether a visit in a status keeps its workers busy at its time.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether it does.</returns>
    public static bool KeepsWorkersBusy(VisitStatus status) => status is not (VisitStatus.Unscheduled or VisitStatus.Canceled);

    /// <summary>
    /// Tells whether a visit in a status can still be given other workers or another time.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>Whether it can.</returns>
    public static bool CanAssign(VisitStatus status) => status is VisitStatus.Unscheduled or VisitStatus.Scheduled or VisitStatus.Dispatched;

    /// <summary>
    /// Gives the words people read for a status, such as "On site".
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The words.</returns>
    public static string Text(this VisitStatus status) => status switch
    {
        VisitStatus.EnRoute => "En route",
        VisitStatus.OnSite => "On site",
        _ => status.ToString(),
    };
}
