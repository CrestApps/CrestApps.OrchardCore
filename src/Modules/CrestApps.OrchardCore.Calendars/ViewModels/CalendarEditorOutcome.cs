namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// What the calendar view's event modal gets back when a change is done: whether it worked and what to tell the user.
/// </summary>
public sealed class CalendarEditorOutcome
{
    /// <summary>
    /// Gets or sets whether the change was made.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the message for the user.
    /// </summary>
    public string Message { get; set; }
}
