using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// Asks for a page of a record's timeline, newest first.
/// </summary>
public sealed class TimelineQuery
{
    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public RecordRef Subject { get; set; }

    /// <summary>
    /// Gets or sets the kinds to include; empty for every kind.
    /// </summary>
    public IReadOnlyCollection<string> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether internal entries are included.
    /// </summary>
    public bool IncludeInternal { get; set; }

    /// <summary>
    /// Gets or sets how many entries to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many entries to return, at most.
    /// </summary>
    public int Take { get; set; } = 50;
}
