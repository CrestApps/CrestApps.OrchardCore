namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The rule of one day of a weekly schedule.
/// </summary>
public sealed class DaySchedule
{
    /// <summary>
    /// Gets or sets whether the day allows all day, nothing, or a time range.
    /// </summary>
    public DayScheduleMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the start of the range, in the site time zone.
    /// </summary>
    public TimeOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the end of the range, in the site time zone.
    /// </summary>
    public TimeOnly? To { get; set; }
}
