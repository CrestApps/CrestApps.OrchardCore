namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Event fields sent by the API or an AI tool. Fields left <see langword="null"/> keep their value on an update.
/// </summary>
public sealed class CalendarEventInput
{
    /// <summary>
    /// Gets or sets the calendar, for a new event.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the event type's technical name.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets where the event happens.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days.
    /// </summary>
    public bool? AllDay { get; set; }

    /// <summary>
    /// Gets or sets the start: an ISO 8601 time with an offset (a moment) or without one (a wall-clock time in the event's time
    /// zone), or a date for an all-day event.
    /// </summary>
    public string Start { get; set; }

    /// <summary>
    /// Gets or sets the end, like <see cref="Start"/>; for an all-day event, the last day.
    /// </summary>
    public string End { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the status: Confirmed, Tentative or Cancelled.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the event takes time: Busy or Free.
    /// </summary>
    public string Transparency { get; set; }

    /// <summary>
    /// Gets or sets an RFC 5545 rule, or an empty string to stop repeating.
    /// </summary>
    public string Recurrence { get; set; }

    /// <summary>
    /// Gets or sets the attendees, each an email, <c>Name &lt;email&gt;</c> or <c>user:{userId}</c>; replaces the attendees.
    /// </summary>
    public IList<string> Attendees { get; set; }
}
