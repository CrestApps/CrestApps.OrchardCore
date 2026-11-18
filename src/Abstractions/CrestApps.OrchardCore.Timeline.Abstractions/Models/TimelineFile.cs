using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// A file attached to a timeline entry, as the timeline's file index keeps it: which entry, on which record, who may
/// read it and who added it when. The file's own name, type and size are the attachment's.
/// </summary>
public sealed class TimelineFile
{
    /// <summary>
    /// Gets or sets the attachment.
    /// </summary>
    public string AttachmentId { get; set; }

    /// <summary>
    /// Gets or sets the entry the file is attached to.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the record the entry is on.
    /// </summary>
    public RecordRef Subject { get; set; }

    /// <summary>
    /// Gets or sets the entry's kind, such as a reply or a note.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets who may read the entry, and so the file.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets who wrote the entry, by name; empty for the site.
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets when the entry was added, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Which files of timelines to list.
/// </summary>
public sealed class TimelineFileQuery
{
    /// <summary>
    /// Gets or sets the records whose files are listed, such as a ticket and the tickets merged into it.
    /// </summary>
    public IReadOnlyCollection<RecordRef> Subjects { get; set; } = [];

    /// <summary>
    /// Gets or sets the records whose internal entries' files are listed too; of the others, only the files of public
    /// entries are.
    /// </summary>
    public IReadOnlyCollection<RecordRef> InternalSubjects { get; set; } = [];

    /// <summary>
    /// Gets or sets how many to skip, newest first.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many to take.
    /// </summary>
    public int Take { get; set; } = 50;
}

/// <summary>
/// A page of timeline files, newest first, with how many there are in all. Files of redacted entries are never listed.
/// </summary>
public sealed class TimelineFilePage
{
    /// <summary>
    /// Gets or sets the files of the page.
    /// </summary>
    public IReadOnlyList<TimelineFile> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets how many files the query finds in all.
    /// </summary>
    public int Total { get; set; }
}
