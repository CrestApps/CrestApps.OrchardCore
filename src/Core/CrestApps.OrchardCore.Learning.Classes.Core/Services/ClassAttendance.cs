using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Learning.Classes.Core.Services;

/// <summary>
/// Decides whether the attendance of a registration counts for its class.
/// </summary>
internal static class ClassAttendance
{
    /// <summary>
    /// Tells whether a registration was attended, or partly attended for at least the class's minimum minutes.
    /// </summary>
    /// <param name="appointment">The registration.</param>
    /// <param name="part">The class, or <see langword="null"/> when it is not found.</param>
    /// <returns><see langword="true"/> when the attendance counts.</returns>
    public static bool Counts(Appointment appointment, ClassPart part)
        => appointment.Attendance == AppointmentAttendance.Attended ||
            (appointment.Attendance == AppointmentAttendance.Partial && part?.MinimumMinutes is int minimum && (appointment.MinutesAttended ?? 0) >= minimum);
}
