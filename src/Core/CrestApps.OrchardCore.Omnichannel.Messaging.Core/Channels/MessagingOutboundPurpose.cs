namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Why an outbound message is being sent, so a channel can apply what its medium expects of each kind: an email
/// channel marks an automatic message as such, so the recipient's auto-responder stays silent, and adds an unsubscribe
/// link to bulk mail.
/// </summary>
public enum MessagingOutboundPurpose
{
    /// <summary>
    /// A person wrote the message in a conversation.
    /// </summary>
    Reply,

    /// <summary>
    /// The workspace acknowledged an inbound message on its own, such as an entry point's auto-reply.
    /// </summary>
    AutoReply,

    /// <summary>
    /// The message is one of many sent to a list of recipients.
    /// </summary>
    Broadcast,

    /// <summary>
    /// Automation, such as an AI agent or a workflow, wrote the message in answer to the contact.
    /// </summary>
    Automation,

    /// <summary>
    /// Automation reached out without the contact having written first: a campaign's opening message or a follow-up
    /// nudge to a contact who went quiet. Bulk-mail rules (an unsubscribe link) apply to it as to a broadcast.
    /// </summary>
    Outreach,
}
