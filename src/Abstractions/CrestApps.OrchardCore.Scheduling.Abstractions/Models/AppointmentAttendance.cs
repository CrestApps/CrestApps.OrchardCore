namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// How a person attended an appointment or a session.
/// </summary>
public enum AppointmentAttendance
{
    /// <summary>
    /// Not marked yet.
    /// </summary>
    Unmarked,

    /// <summary>
    /// Attended the whole time.
    /// </summary>
    Attended,

    /// <summary>
    /// Attended part of the time.
    /// </summary>
    Partial,

    /// <summary>
    /// Did not attend.
    /// </summary>
    Absent,
}
