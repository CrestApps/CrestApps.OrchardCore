using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

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
    /// Gets or sets the identifier of the workspace conversation the message belongs to, when it belongs to one. A
    /// channel that threads its messages (an email channel's <c>In-Reply-To</c> and <c>Re:</c> subject) reads the
    /// conversation's earlier messages through it.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the inbound message this message answers, when the sender knows it: an auto-reply or an AI agent
    /// answers the message that just arrived, which may not be stored in the conversation yet. When it is not set, a
    /// threading channel answers the conversation's latest inbound message.
    /// </summary>
    public OmnichannelMessage ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets why the message is being sent.
    /// </summary>
    public MessagingOutboundPurpose Purpose { get; set; }

    /// <summary>
    /// Gets or sets the subject line, on a channel that supports one. A channel may derive one when it is empty, as
    /// an email channel does from the conversation it replies on.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the public links to the media to attach, on a channel whose provider downloads the files itself
    /// (see <see cref="MessagingAttachmentCapabilities.DeliveredAsLinks"/>).
    /// </summary>
    public IList<string> MediaUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the files to attach, kept in the attachment store, so a channel that embeds files in the message
    /// itself (an email) reads them from the store rather than downloading its own links.
    /// </summary>
    public IList<MessagingAttachment> Attachments { get; set; } = [];
}
