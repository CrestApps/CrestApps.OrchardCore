using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars.Serialization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A shift in a draft scenario, in the team's wall-clock time. Published, it becomes a Shift event on the team's calendar that keeps its <see cref="ShiftKey"/>.
/// </summary>
public sealed class DraftShift : CatalogItem
{
    /// <summary>
    /// Gets or sets the scenario.
    /// </summary>
    public string ScenarioId { get; set; }

    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker, or <see langword="null"/> for an open shift nobody works yet.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the day the shift belongs to, the day it starts.
    /// </summary>
    public DateOnly Date { get; set; }

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

    /// <summary>
    /// Gets or sets the time zone of the wall-clock times, the team's.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the template it was made from.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the breaks and other activities inside it.
    /// </summary>
    public IList<DraftShiftSegment> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets a key that stays the same across publishes, so a republish updates the same calendar event.
    /// </summary>
    public string ShiftKey { get; set; }
}
