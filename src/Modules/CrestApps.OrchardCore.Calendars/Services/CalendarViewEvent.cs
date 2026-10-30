namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// An occurrence as the calendar view's script reads it.
/// </summary>
public sealed class CalendarViewEvent
{
    /// <summary>
    /// Gets or sets the occurrence id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the event's id.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the title shown; "Busy" when the viewer may only see busy time.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the start: an ISO 8601 UTC time, or a date for an all-day occurrence.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets the end: an ISO 8601 UTC time, or the day after the last day for an all-day occurrence.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets whether the occurrence lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets the color to draw it in.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may drag it to another time.
    /// </summary>
    public bool Editable { get; set; }

    /// <summary>
    /// Gets or sets details shown when it is clicked.
    /// </summary>
    public CalendarViewEventDetails Details { get; set; }
}
