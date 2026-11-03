using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Endpoints;

/// <summary>
/// A request from the calendar view to move or resize an occurrence.
/// </summary>
public sealed class CalendarMoveRequest
{
    /// <summary>
    /// Gets or sets the event's id.
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Gets or sets when the occurrence was due to start, for an occurrence of a repeating event.
    /// </summary>
    public DateTime? OriginalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the new start, in UTC, or the new first day for an all-day occurrence.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the new end, in UTC, or the day after the new last day for an all-day occurrence.
    /// </summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// Gets or sets whether the occurrence now lasts whole days.
    /// </summary>
    public bool AllDay { get; set; }

    /// <summary>
    /// Gets or sets which occurrences of a repeating event to move.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CalendarEditScope>))]
    public CalendarEditScope Scope { get; set; }
}
