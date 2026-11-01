namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a workforce notification is about.
/// </summary>
public enum WorkforceNotificationKind
{
    /// <summary>
    /// A schedule with the worker's shifts was published.
    /// </summary>
    SchedulePublished,

    /// <summary>
    /// One of the worker's published shifts was added, moved, removed or given to someone else.
    /// </summary>
    ShiftChanged,

    /// <summary>
    /// A decision on the worker's time off.
    /// </summary>
    TimeOff,

    /// <summary>
    /// An open shift the worker claimed was given to them, or to someone else.
    /// </summary>
    OpenShift,

    /// <summary>
    /// A shift trade offered to the worker, or a decision on one they offered.
    /// </summary>
    ShiftTrade,

    /// <summary>
    /// A timesheet approved, sent back or ready to submit.
    /// </summary>
    Timesheet,

    /// <summary>
    /// An attendance exception, or a decision on a punch correction.
    /// </summary>
    Attendance,
}
