namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// The pictures of one message, kept on the message's property bag. A received message also records the media
/// the provider announced that could not be kept (not a picture, too large, or unreachable), so the thread can say
/// something was sent rather than show a message with nothing in it.
/// </summary>
public sealed class MessagingMessageAttachments
{
    /// <summary>
    /// Gets or sets the stored pictures, in the order they were sent.
    /// </summary>
    public IList<MessagingAttachment> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets how many of the media items the provider announced were not stored.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider's media for a received message has been fetched, so a
    /// message processed twice is not fetched twice.
    /// </summary>
    public bool Ingested { get; set; }
}
