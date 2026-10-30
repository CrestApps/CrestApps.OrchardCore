using System.Security.Claims;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// What <see cref="ICalendarQueryService.GetOccurrencesAsync"/> reads.
/// </summary>
public sealed class CalendarOccurrenceQuery
{
    /// <summary>
    /// Gets or sets the calendars whose events to read.
    /// </summary>
    public IList<string> CalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to read events on every calendar, for background work such as reminders. Bounded by
    /// <see cref="CalendarOptions.MaxEventsPerQuery"/>.
    /// </summary>
    public bool AllCalendars { get; set; }

    /// <summary>
    /// Gets or sets users whose invitations to read, on any calendar. Declined invitations are left out.
    /// </summary>
    public IList<string> AttendeeUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets resource calendars whose bookings on other calendars to read.
    /// </summary>
    public IList<string> ResourceCalendarIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the start of the range, in UTC.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the end of the range (exclusive), in UTC.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets whether to include cancelled events.
    /// </summary>
    public bool IncludeCancelled { get; set; }

    /// <summary>
    /// Gets or sets whether to include events that leave the time free.
    /// </summary>
    public bool IncludeFree { get; set; } = true;

    /// <summary>
    /// Gets or sets the event types to match, or none for every type.
    /// </summary>
    public IList<string> EventTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the owning modules to match, or none for every source.
    /// </summary>
    public IList<string> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to add the occurrences other modules contribute through <see cref="ICalendarEventSource"/>.
    /// </summary>
    public bool IncludeEventSources { get; set; }

    /// <summary>
    /// Gets or sets who is looking. When set, occurrences on calendars the viewer can't read are reduced to busy time, and those on
    /// calendars the viewer can't see at all are left out, unless the viewer is invited.
    /// </summary>
    public ClaimsPrincipal Viewer { get; set; }
}
