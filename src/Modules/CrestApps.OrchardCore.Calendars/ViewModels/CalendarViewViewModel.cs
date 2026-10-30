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
}
