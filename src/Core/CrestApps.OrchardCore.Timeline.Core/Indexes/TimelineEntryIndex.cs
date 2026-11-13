using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Core.Indexes;

/// <summary>
/// Indexes timeline entries by their record, kind, visibility, author and time.
/// </summary>
public sealed class TimelineEntryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the record's kind.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the record's id.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the entry's kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets who may read the entry.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the author's party kind.
    /// </summary>
    public string AuthorKind { get; set; }

    /// <summary>
    /// Gets or sets the author's party id.
    /// </summary>
    public string AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets when the entry was written, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the body was redacted.
    /// </summary>
    public bool Redacted { get; set; }
}
