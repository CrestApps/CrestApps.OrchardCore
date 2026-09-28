using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// A request to send an outbound message on a conversation.
/// </summary>
public sealed class MessagingSendRequest
{
    /// <summary>
    /// Gets or sets the identifier of the conversation to reply on.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the subject line, on a channel that supports one.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the media URLs to attach, on a channel that supports media.
    /// </summary>
    public IList<string> MediaUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the agent sending the message. Null for a system-sent message such as an
    /// auto-reply.
    /// </summary>
    public string ActingAgentId { get; set; }

    /// <summary>
    /// Gets or sets the principal the send is authorized as. When set, the conversation-level rule is enforced
    /// again inside the service, so a caller that skipped the controller check still cannot send on a thread it
    /// does not own. Null for a system-sent message such as an auto-reply.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }
}

/// <summary>
/// The outcome of a send.
/// </summary>
public sealed class MessagingSendResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider accepted the message.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the persisted outbound message, when one was created.
    /// </summary>
    public OmnichannelMessage Message { get; set; }

    /// <summary>
    /// Gets or sets the error text when the send failed or was refused.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Creates a failed result carrying the given error.
    /// </summary>
    public static MessagingSendResult Failed(string error) => new() { Succeeded = false, Error = error };
}

/// <summary>
/// A normalized delivery receipt from a provider webhook.
/// </summary>
public sealed class MessageDeliveryReceipt
{
    /// <summary>
    /// Gets or sets the technical name of the channel the message was sent on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets our address the message was sent from.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact address the message was sent to.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the provider's message identifier, when known.
    /// </summary>
    public string ProviderMessageId { get; set; }

    /// <summary>
    /// Gets or sets the normalized delivery status.
    /// </summary>
    public MessageDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error code, when the message failed.
    /// </summary>
    public string ErrorCode { get; set; }
}
