namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// A stretch of time that is taken.
/// </summary>
public sealed class CalendarBusyInterval
{
    /// <summary>
    /// Gets or sets when the busy time starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the busy time ends (exclusive), in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the calendar the busy time comes from, when it comes from a calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the user the busy time belongs to, when it was asked for by user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the event the busy time comes from, when it comes from an event.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets what contributed the busy time: <c>Calendar</c> for events, or a busy time source's name.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets whether the busy time is only tentative.
    /// </summary>
    public bool Tentative { get; set; }
}
