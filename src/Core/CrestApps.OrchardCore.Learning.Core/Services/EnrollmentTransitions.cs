using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The moves between enrollment states. Completed and withdrawn are final; an expired enrollment becomes active again
/// when its access is extended.
/// </summary>
public static class EnrollmentTransitions
{
    private static readonly TransitionTable<EnrollmentStatus> _table = new(new Dictionary<EnrollmentStatus, EnrollmentStatus[]>
    {
        [EnrollmentStatus.Pending] = [EnrollmentStatus.Active, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Active] = [EnrollmentStatus.Suspended, EnrollmentStatus.Completed, EnrollmentStatus.Expired, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Suspended] = [EnrollmentStatus.Active, EnrollmentStatus.Expired, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Expired] = [EnrollmentStatus.Active, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Completed] = [],
        [EnrollmentStatus.Withdrawn] = [],
    });

    /// <summary>
    /// Gets whether an enrollment may move from one state to another.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The new state.</param>
    /// <returns><see langword="true"/> when the move is allowed.</returns>
    public static bool CanMove(EnrollmentStatus from, EnrollmentStatus to)
        => _table.CanMove(from, to);

    /// <summary>
    /// Gets whether a state is final.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <returns><see langword="true"/> when no move leaves it.</returns>
    public static bool IsTerminal(EnrollmentStatus status)
        => _table.IsTerminal(status);
}
