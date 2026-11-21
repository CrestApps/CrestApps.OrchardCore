namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// The days and hours responses may start, in the site time zone.
/// </summary>
public sealed class WeeklySchedule
{
    /// <summary>
    /// Gets or sets the rule of each day; a day without one allows all day.
    /// </summary>
    public Dictionary<DayOfWeek, DaySchedule> Days { get; set; } = [];
}
