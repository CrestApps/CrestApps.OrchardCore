using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// A stored calendar event as the calendar events data set reads it, with its calendar's type and whether the
/// principal may see its details.
/// </summary>
internal sealed class CalendarEventReportRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventReportRow"/> class.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    public CalendarEventReportRow(CalendarEvent calendarEvent)
    {
        Event = calendarEvent;
    }

    /// <summary>
    /// Gets the event.
    /// </summary>
    public CalendarEvent Event { get; }

    /// <summary>
    /// Gets or sets the type of the event's calendar.
    /// </summary>
    public string CalendarType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the principal may only see that the time is busy, so the event's title
    /// and attendees are left empty.
    /// </summary>
    public bool DetailsHidden { get; set; }
}
