namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The page that asks which calendar and kind of event to add.
/// </summary>
public class ChooseCalendarViewModel
{
    /// <summary>
    /// Gets or sets the calendars the viewer may add to, with their event types.
    /// </summary>
    public IList<ChooseCalendarEntry> Calendars { get; set; } = [];

    /// <summary>
    /// Gets or sets the start passed on to the editor.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets the end passed on to the editor.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }
}
