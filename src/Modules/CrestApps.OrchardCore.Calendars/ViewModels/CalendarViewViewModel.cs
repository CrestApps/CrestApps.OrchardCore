namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The calendar view page.
/// </summary>
public class CalendarViewViewModel
{
    /// <summary>
    /// Gets or sets the calendars the viewer can see.
    /// </summary>
    public IList<CalendarViewCalendar> Calendars { get; set; } = [];

    /// <summary>
    /// Gets or sets the settings the page's script reads, as JSON.
    /// </summary>
    public string ConfigurationJson { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may create calendars.
    /// </summary>
    public bool CanCreateCalendars { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may see other people's schedules.
    /// </summary>
    public bool CanViewSchedules { get; set; }

    /// <summary>
    /// Gets or sets whose schedule is shown: empty for the viewer's calendars, <c>everyone</c>, or <c>person</c>.
    /// </summary>
    public string ScheduleMode { get; set; }

    /// <summary>
    /// Gets or sets the name of the person whose schedule is shown.
    /// </summary>
    public string ScheduleName { get; set; }

    /// <summary>
    /// Gets or sets the person whose schedule is shown, as the user picker's initial items JSON.
    /// </summary>
    public string SchedulePersonJson { get; set; } = "[]";
}
