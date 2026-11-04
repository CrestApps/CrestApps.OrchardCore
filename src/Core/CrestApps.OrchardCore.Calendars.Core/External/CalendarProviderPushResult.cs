namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// The external copy an <see cref="IWritableCalendarProvider"/> wrote.
/// </summary>
public sealed class CalendarProviderPushResult
{
    /// <summary>
    /// Gets or sets the external event's id. Later reads must return the event with this id as its
    /// <see cref="ICalendar.ICalendarEventData.Uid"/>, so the two copies stay matched.
    /// </summary>
    public string ExternalEventId { get; set; }

    /// <summary>
    /// Gets or sets the external copy's new version marker (an ETag or a change key).
    /// </summary>
    public string Version { get; set; }
}
