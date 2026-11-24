using CrestApps.OrchardCore.Timeline.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Timeline.Core.Indexes;

/// <summary>
/// Indexes the files attached to timeline entries, one row per file, so a record lists all its files without reading its
/// entries.
/// </summary>
public sealed class TimelineAttachmentIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the attachment.
    /// </summary>
    public string AttachmentId { get; set; }

    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the kind of the record the entry is on.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the entry is on.
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
    /// Gets or sets who wrote the entry, by name.
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets when the entry was added, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the entry was redacted.
    /// </summary>
    public bool Redacted { get; set; }
}
