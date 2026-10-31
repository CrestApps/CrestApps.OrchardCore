using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A part of a draft shift spent on one activity, in the team's wall-clock time.
/// </summary>
public sealed class DraftShiftSegment
{
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets when it starts, wall-clock.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets when it ends, wall-clock.
    /// </summary>
    [JsonConverter(typeof(WallClockDateTimeJsonConverter))]
    public DateTime End { get; set; }
}
