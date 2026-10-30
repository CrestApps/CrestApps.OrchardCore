namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A window of working hours on a day of the week. Several windows on one day leave breaks between them.
/// </summary>
public sealed class SchedulingWorkingHours
{
    /// <summary>
    /// Gets or sets the day of the week.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets when the window starts, as HH:mm.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets when the window ends, as HH:mm.
    /// </summary>
    public string End { get; set; }
}
