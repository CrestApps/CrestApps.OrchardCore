using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The moves between enrollment states. Completed and withdrawn are final; an expired enrollment becomes active again
/// when its access is extended.
/// </summary>
public static class EnrollmentTransitions
{
    /// <summary>
    /// Gets the transition table.
    /// </summary>
    public static TransitionTable<EnrollmentStatus> Table { get; } = new(new Dictionary<EnrollmentStatus, EnrollmentStatus[]>
    {
        [EnrollmentStatus.Pending] = [EnrollmentStatus.Active, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Active] = [EnrollmentStatus.Suspended, EnrollmentStatus.Completed, EnrollmentStatus.Expired, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Suspended] = [EnrollmentStatus.Active, EnrollmentStatus.Expired, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Expired] = [EnrollmentStatus.Active, EnrollmentStatus.Withdrawn],
        [EnrollmentStatus.Completed] = [],
        [EnrollmentStatus.Withdrawn] = [],
    });
}
