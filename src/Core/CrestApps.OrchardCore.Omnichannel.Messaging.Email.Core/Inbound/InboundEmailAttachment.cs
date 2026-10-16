namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// A file attached to a received email.
/// </summary>
public sealed class InboundEmailAttachment
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type the sender declared.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public byte[] Content { get; set; } = [];

    /// <summary>
    /// Gets or sets the part's <c>Content-ID</c>, when the HTML body shows it inline (a logo in a signature, a pasted
    /// screenshot).
    /// </summary>
    public string ContentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the part was marked to be shown inline rather than as an attachment.
    /// </summary>
    public bool IsInline { get; set; }
}
