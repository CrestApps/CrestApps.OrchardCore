using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// One row per attendee of an event, so a user's invitations and a resource's bookings can be found without loading every event.
/// </summary>
public sealed class CalendarEventAttendeeIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the event's id.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets the event's calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the attendee's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the resource calendar the attendee stands for.
    /// </summary>
    public string ResourceCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the attendee's email in lower case.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the attendee's answer.
    /// </summary>
    public CalendarAttendeeResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the event's state.
    /// </summary>
    public CalendarEventStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the event starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event's last occurrence ends, in UTC.
    /// </summary>
    public DateTime RangeEndUtc { get; set; }
}
