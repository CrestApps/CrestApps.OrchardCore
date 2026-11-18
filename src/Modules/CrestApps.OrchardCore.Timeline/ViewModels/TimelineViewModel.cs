namespace CrestApps.OrchardCore.Timeline.ViewModels;

/// <summary>
/// A record's timeline as the timeline card shows it.
/// </summary>
public class TimelineViewModel
{
    /// <summary>
    /// Gets or sets the record, as <c>Kind:Id</c>.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the record's kind, for the composer shape's alternate.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the kinds of entry the user may add, one button each.
    /// </summary>
    public IList<TimelineComposerButton> Composer { get; set; } = [];

    /// <summary>
    /// Gets or sets the text a post that was refused kept, so the composer shows it again.
    /// </summary>
    public string Draft { get; set; }

    /// <summary>
    /// Gets or sets the page of the timeline shown, from 1 for the newest entries.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the address of the page of newer entries, or <see langword="null"/> on the first page.
    /// </summary>
    public string NewerUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the page of older entries, or <see langword="null"/> on the last page.
    /// </summary>
    public string OlderUrl { get; set; }

    /// <summary>
    /// Gets or sets the card's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the page to come back to after a post.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets how many entries the record has in all.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets whether the user may redact an entry.
    /// </summary>
    public bool CanRedact { get; set; }

    /// <summary>
    /// Gets or sets the entries, newest first.
    /// </summary>
    public IList<TimelineEntryViewModel> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets whether files can be attached to a new entry.
    /// </summary>
    public bool CanAttach { get; set; }
}
