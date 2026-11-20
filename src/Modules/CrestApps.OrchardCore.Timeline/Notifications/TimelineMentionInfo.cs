using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Notifications;

/// <summary>
/// What a mention notification is about, kept on the notification for its visibility.
/// </summary>
public sealed class TimelineMentionInfo
{
    /// <summary>
    /// Gets or sets the record the entry is on, as <c>Kind:Id</c>.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the entry.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets who may read the entry.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }
}
