using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// The YesSql index of <see cref="CalendarEvent"/> documents. <see cref="RangeEndUtc"/> is the end of the last occurrence, so a
/// range query finds repeating events by <c>StartUtc &lt; to AND RangeEndUtc &gt; from</c>.
/// </summary>
public sealed class CalendarEventIndex : CatalogItemIndex
{
    /// <summary>
    /// The <see cref="RangeEndUtc"/> of a series that never ends.
    /// </summary>
    public static readonly DateTime Forever = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets the event's calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the event's type.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the event's state.
    /// </summary>
    public CalendarEventStatus Status { get; set; }

    /// <summary>
    /// Gets or sets whether the event takes time.
    /// </summary>
    public CalendarTransparency Transparency { get; set; }

    /// <summary>
    /// Gets or sets when the event (its first occurrence) starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event (its first occurrence) ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets when the event's last occurrence ends, in UTC; <see cref="Forever"/> for a series that never ends.
    /// </summary>
    public DateTime RangeEndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the event repeats.
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Gets or sets the series an override belongs to.
    /// </summary>
    public string SeriesId { get; set; }

    /// <summary>
    /// Gets or sets when the overridden occurrence was due to start.
    /// </summary>
    public DateTime? OriginalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the module that owns the event.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in the owning module.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the event's iCalendar UID.
    /// </summary>
    public string Uid { get; set; }
}
