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
    /// Gets or sets whether the user may add an internal note.
    /// </summary>
    public bool CanAddNote { get; set; }

    /// <summary>
    /// Gets or sets whether the user may add a public comment.
    /// </summary>
    public bool CanAddComment { get; set; }

    /// <summary>
    /// Gets or sets whether the user may redact an entry.
    /// </summary>
    public bool CanRedact { get; set; }

    /// <summary>
    /// Gets or sets the entries, newest first.
    /// </summary>
    public IList<TimelineEntryViewModel> Entries { get; set; } = [];
}
