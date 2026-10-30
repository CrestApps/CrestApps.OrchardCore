using System.Globalization;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// One occurrence of an event in a range: the event itself for a one-time event, or one expanded occurrence of a repeating event.
/// </summary>
public sealed class CalendarOccurrence
{
    /// <summary>
    /// Gets or sets the event the occurrence comes from: the override when the occurrence was changed, otherwise the event or series.
    /// </summary>
    public CalendarEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the id of the repeating event, or the event's own id for a one-time event.
    /// </summary>
    public string SeriesId { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence was due to start by the series rule, in UTC. Identifies the occurrence within its series.
    /// </summary>
    public DateTime OriginalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence starts, as a wall-clock time in the event's time zone.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence ends, as a wall-clock time in the event's time zone.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime End { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the occurrence belongs to a repeating event.
    /// </summary>
    public bool IsRecurring { get; set; }

    /// <summary>
    /// Gets or sets whether the occurrence was changed from the series.
    /// </summary>
    public bool IsOverride { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may only see that the time is busy. When <see langword="true"/>, show no title or details.
    /// </summary>
    public bool BusyOnly { get; set; }

    /// <summary>
    /// Gets the id that identifies the occurrence: the event id for a one-time event, otherwise the series id and the original start.
    /// </summary>
    /// <returns>The occurrence id.</returns>
    public string GetOccurrenceId()
    {
        return IsRecurring
            ? SeriesId + "_" + OriginalStartUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
            : SeriesId;
    }
}
