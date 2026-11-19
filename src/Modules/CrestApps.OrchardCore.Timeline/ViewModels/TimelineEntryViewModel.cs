using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.ViewModels;

/// <summary>
/// One entry of the timeline card.
/// </summary>
public class TimelineEntryViewModel
{
    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public TimelineEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets the author's name, or <see langword="null"/> for the site itself.
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets the entry's files that are still stored.
    /// </summary>
    public IList<TimelineAttachmentViewModel> Attachments { get; set; } = [];
}
