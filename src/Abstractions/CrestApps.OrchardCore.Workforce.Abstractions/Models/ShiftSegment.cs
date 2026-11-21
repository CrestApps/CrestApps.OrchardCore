namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A part of a shift spent on one activity, such as a break, in UTC.
/// </summary>
public sealed class ShiftSegment
{
    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the activity's kind when the shift was saved, so readers need not load the code.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets when the segment starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the segment ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets what placed the segment, such as another module, so it can take it back; empty for a segment a
    /// planner added.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record of <see cref="Source"/> that placed the segment, so it can take it back.
    /// </summary>
    public string SourceId { get; set; }
}
