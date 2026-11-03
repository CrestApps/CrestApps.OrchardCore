using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// A <c>VEVENT</c> read from an iCalendar document.
/// </summary>
public sealed class ICalendarEventData
{
    /// <summary>
    /// Gets or sets the UID.
    /// </summary>
    public string Uid { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the location.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets the start: a wall-clock time in <see cref="TimeZoneId"/>, or a date for an all-day event.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the end, or <see langword="null"/> when the event names none.
    /// </summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// Gets or sets the time zone of the start and end, or <see langword="null"/> for a floating time.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the recurrence rule.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets the removed occurrences, as wall-clock times in <see cref="TimeZoneId"/>.
    /// </summary>
    public IList<DateTime> ExceptionDates { get; set; } = [];

    /// <summary>
    /// Gets or sets the occurrence this event changes, in UTC, for an override.
    /// </summary>
    public DateTime? RecurrenceIdUtc { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public CalendarEventStatus Status { get; set; }

    /// <summary>
    /// Gets or sets whether the event takes time.
    /// </summary>
    public CalendarTransparency Transparency { get; set; }

    /// <summary>
    /// Gets or sets the sequence number.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets when the event was last changed, in UTC.
    /// </summary>
    public DateTime? LastModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the attendees.
    /// </summary>
    public IList<CalendarAttendee> Attendees { get; set; } = [];

    /// <summary>
    /// Gets or sets the external copy's version marker (an ETag or a change key), for a provider that has one.
    /// </summary>
    public string Version { get; set; }
}
