using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Calendars.Models;

/// <summary>
/// How an event repeats.
/// </summary>
public sealed class CalendarRecurrence
{
    /// <summary>
    /// Gets or sets the RFC 5545 recurrence rule without the <c>RRULE:</c> prefix, such as <c>FREQ=WEEKLY;BYDAY=MO,WE;COUNT=10</c>.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets the wall-clock starts of occurrences that were removed from the series.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeListJsonConverter))]
    public IList<DateTime> ExceptionDates { get; set; } = [];

    /// <summary>
    /// Gets or sets when the last occurrence ends, in UTC, or <see langword="null"/> when the series never ends. Derived from the rule.
    /// </summary>
    public DateTime? EndUtc { get; set; }
}
