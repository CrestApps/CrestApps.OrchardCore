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
}
