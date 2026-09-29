namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// One picture carried by a message, as the workspace stores it. The bytes live encrypted in the
/// <see cref="IMessagingAttachmentStore"/>, addressed by <see cref="Id"/>; the message keeps only this description,
/// so a thread loads without reading any image.
/// </summary>
public sealed class MessagingAttachment
{
    /// <summary>
    /// Gets or sets the key the bytes are stored under in the attachment store.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the media type, as detected from the bytes themselves rather than as the sender claimed it.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file name the agent attached, when there was one. It is only shown, never used as a path.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the size of the stored picture in bytes.
    /// </summary>
    public long Length { get; set; }
}
