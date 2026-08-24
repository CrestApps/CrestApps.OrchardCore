using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// A request to send an outbound message on a conversation.
/// </summary>
public sealed class SmsSendRequest
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
    /// Gets or sets the media URLs to attach (MMS). Text-only 1:1 is the phase-1 MVP; media lands with the
    /// MMS-enabled phase.
    /// </summary>
    public IList<string> MediaUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the agent sending the message. Null for a system-sent message such as an
    /// auto-reply.
    /// </summary>
    public string ActingAgentId { get; set; }
}

/// <summary>
/// The outcome of a send.
/// </summary>
public sealed class SmsSendResult
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
    public static SmsSendResult Failed(string error) => new() { Succeeded = false, Error = error };
}

/// <summary>
/// A normalized delivery receipt from a provider webhook.
/// </summary>
public sealed class SmsDeliveryReceipt
{
    /// <summary>
    /// Gets or sets the DID (our number) the message was sent from.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact number the message was sent to.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the provider's message identifier, when known.
    /// </summary>
    public string ProviderMessageId { get; set; }

    /// <summary>
    /// Gets or sets the normalized delivery status.
    /// </summary>
    public SmsDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error code, when the message failed.
    /// </summary>
    public string ErrorCode { get; set; }
}
