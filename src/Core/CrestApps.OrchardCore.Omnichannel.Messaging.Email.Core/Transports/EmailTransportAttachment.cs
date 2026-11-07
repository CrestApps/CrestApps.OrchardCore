namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// One file attached to an outbound email.
/// </summary>
public sealed class EmailTransportAttachment
{
    /// <summary>
    /// Gets or sets the file name shown to the recipient.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the file's media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file's content.
    /// </summary>
    public byte[] Content { get; set; } = [];
}
