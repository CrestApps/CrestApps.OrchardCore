using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// The state shared by the <see cref="IMessagingInboundHandler"/>s for one inbound message.
/// </summary>
public sealed class MessagingInboundContext
{
    /// <summary>
    /// Gets the channel the message arrived on.
    /// </summary>
    public required IMessagingChannel Channel { get; init; }

    /// <summary>
    /// Gets the inbound message, already normalized.
    /// </summary>
    public required OmnichannelMessage Message { get; init; }

    /// <summary>
    /// Gets the endpoint the message arrived on.
    /// </summary>
    public required OmnichannelChannelEndpoint Endpoint { get; init; }

    /// <summary>
    /// Gets the conversation the message belongs to (found or created before the handlers run).
    /// </summary>
    public required MessagingConversation Conversation { get; init; }

    /// <summary>
    /// Gets a value indicating whether the conversation was created for this message.
    /// </summary>
    public bool IsNewConversation { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether automation, such as the endpoint's auto-reply, must stay silent for
    /// this message because a handler has already given the one answer it is owed.
    /// </summary>
    public bool SuppressAutomatedReplies { get; set; }
}
