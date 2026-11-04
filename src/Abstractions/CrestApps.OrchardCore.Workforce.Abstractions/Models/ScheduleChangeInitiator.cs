namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Who asked for a schedule change.
/// </summary>
public enum ScheduleChangeInitiator
{
    /// <summary>
    /// The employer: a manager or scheduler changed the schedule.
    /// </summary>
    Employer,

    /// <summary>
    /// The worker, such as a swap or a time-off request.
    /// </summary>
    Worker,
}
