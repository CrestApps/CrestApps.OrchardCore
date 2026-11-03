namespace CrestApps.OrchardCore.Calendars.Core.Models;

/// <summary>
/// A reminder of an event for one user, shown on their screen until they dismiss it.
/// </summary>
public sealed class CalendarUserReminder
{
    /// <summary>
    /// Gets or sets the reminder's id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the user to remind.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the event.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name, as it was when the reminder was made.
    /// </summary>
    public string CalendarName { get; set; }

    /// <summary>
    /// Gets or sets the event's title, as it was when the reminder was made.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets where the event is.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence starts.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence ends.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets how many minutes before the start the reminder was for.
    /// </summary>
    public int MinutesBefore { get; set; }

    /// <summary>
    /// Gets or sets when the reminder was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the user dismissed it, or <see langword="null"/> while it is shown.
    /// </summary>
    public DateTime? DismissedUtc { get; set; }
}
