using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A segment another module places on a worker's published shifts, such as training or a meeting, and can take back
/// later by its <see cref="Source"/> and <see cref="SourceId"/>.
/// </summary>
public sealed class ShiftSegmentPlacement
{
    /// <summary>
    /// Gets or sets the worker, a staff member id.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the kind of activity; when <see cref="ActivityCodeId"/> is empty, the first enabled activity code of
    /// this kind is used.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the activity code; optional.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets when the segment starts, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the segment ends, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets what places the segment, so it can take it back.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record of <see cref="Source"/> that places the segment. Placing again with the same source and
    /// id replaces the segment instead of adding another.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets why, for the change log.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; set; }
}
