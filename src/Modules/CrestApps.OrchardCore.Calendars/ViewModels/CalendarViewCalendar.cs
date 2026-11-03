namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// A calendar in the calendar view's list.
/// </summary>
public class CalendarViewCalendar
{
    /// <summary>
    /// Gets or sets the calendar's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the calendar's color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the name of the calendar's type.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the icon of the calendar's type.
    /// </summary>
    public string TypeIcon { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may add events to it.
    /// </summary>
    public bool Writable { get; set; }

    /// <summary>
    /// Gets or sets whether it is shown when the page opens.
    /// </summary>
    public bool Selected { get; set; }
}
