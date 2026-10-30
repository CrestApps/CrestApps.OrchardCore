using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// An event on a calendar. The wall-clock <see cref="Start"/> and <see cref="End"/> in <see cref="TimeZoneId"/> are the truth;
/// <see cref="StartUtc"/> and <see cref="EndUtc"/> are derived from them on every save, so a repeating 9:00 meeting stays at 9:00
/// across daylight-saving changes. Type-specific data lives in <see cref="ExtensibleEntity.Properties"/>.
/// </summary>
public sealed class CalendarEvent : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the calendar the event is on.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the name of the event type, one of the calendar type's event types.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the event's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the event's description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets where the event happens.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets whether the event lasts whole days. An all-day event's <see cref="Start"/> and <see cref="End"/> are dates at
    /// midnight, and <see cref="End"/> is the day after the last day.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets when the event starts, as a wall-clock time in <see cref="TimeZoneId"/>.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when the event ends (exclusive), as a wall-clock time in <see cref="TimeZoneId"/>.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone of <see cref="Start"/> and <see cref="End"/>.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets when the event (the first occurrence, for a repeating event) starts, in UTC. Derived from <see cref="Start"/>.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event (the first occurrence, for a repeating event) ends, in UTC. Derived from <see cref="End"/>.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the event's state.
    /// </summary>
    public CalendarEventStatus Status { get; set; }

    /// <summary>
    /// Gets or sets whether the event takes the time of the calendar's owner and attendees.
    /// </summary>
    public CalendarTransparency Transparency { get; set; }

    /// <summary>
    /// Gets or sets who can see the event's details.
    /// </summary>
    public CalendarEventVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets a color that overrides the event type's and the calendar's color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the people and resources invited to the event.
    /// </summary>
    public IList<CalendarAttendee> Attendees { get; set; } = [];

    /// <summary>
    /// Gets or sets how the event repeats, or <see langword="null"/> when it happens once.
    /// </summary>
    public CalendarRecurrence Recurrence { get; set; }

    /// <summary>
    /// Gets or sets the id of the repeating event this event overrides one occurrence of, or <see langword="null"/>.
    /// </summary>
    public string SeriesId { get; set; }

    /// <summary>
    /// Gets or sets when the overridden occurrence was due to start, in UTC, for an event with a <see cref="SeriesId"/>.
    /// </summary>
    public DateTime? OriginalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the module that owns the event, such as <c>Scheduling</c> or <c>Bookings</c>, or <see langword="null"/>
    /// when people own it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id of the record in <see cref="Source"/> the event stands for.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets whether people may change the event in the calendar. A module that owns an event usually sets this to
    /// <see langword="false"/> and changes the event itself.
    /// </summary>
    public bool Editable { get; set; } = true;

    /// <summary>
    /// Gets or sets the iCalendar UID, kept stable across exports and synchronization. Defaults to the event's id.
    /// </summary>
    public string Uid { get; set; }

    /// <summary>
    /// Gets or sets the iCalendar sequence number, raised on each change that matters to attendees.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the event's copies in external calendars.
    /// </summary>
    public IList<CalendarProviderMapping> ProviderMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets when the event was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event was last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who created the event.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who last changed the event, or <c>workflow</c> when a workflow did.
    /// </summary>
    public string ModifiedBy { get; set; }
}
