namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What went wrong with attendance.
/// </summary>
public enum AttendanceExceptionKind
{
    /// <summary>
    /// Clocked in after the shift started, past the grace.
    /// </summary>
    Late,

    /// <summary>
    /// Clocked out before the shift ended, past the grace.
    /// </summary>
    EarlyOut,

    /// <summary>
    /// A punch is missing, such as clocking in and never out.
    /// </summary>
    MissedPunch,

    /// <summary>
    /// A scheduled shift with no punches and no time off.
    /// </summary>
    NoShow,

    /// <summary>
    /// Time away that wasn't planned.
    /// </summary>
    UnscheduledAbsence,

    /// <summary>
    /// A meal longer than the policy allows.
    /// </summary>
    LongMeal,

    /// <summary>
    /// A punch from outside the work site.
    /// </summary>
    OutsideGeofence,
}
