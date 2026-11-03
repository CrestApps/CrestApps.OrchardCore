namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// A calendar and the kinds of events it holds.
/// </summary>
public class ChooseCalendarEntry
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
    /// Gets or sets the icon of the calendar's type.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the event types, as name and display name.
    /// </summary>
    public IList<KeyValuePair<string, string>> EventTypes { get; set; } = [];
}
