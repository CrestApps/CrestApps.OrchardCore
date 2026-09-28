namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// One outbound message handed to a channel to deliver.
/// </summary>
public sealed class MessagingOutboundMessage
{
    /// <summary>
    /// Gets or sets our address the message leaves from, which selects the endpoint and so the provider.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact's address the message is delivered to.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the subject line, on a channel that supports one.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the media to attach, on a channel that supports media.
    /// </summary>
    public IList<string> MediaUrls { get; set; } = [];
}
