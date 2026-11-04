namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What happens to a worker's published shifts when their time off is approved.
/// </summary>
public enum TimeOffShiftHandling
{
    /// <summary>
    /// The shifts stay; the request lists them so the scheduler can find cover.
    /// </summary>
    Flag,

    /// <summary>
    /// The shifts are removed from the schedule and the calendar.
    /// </summary>
    Remove,
}
