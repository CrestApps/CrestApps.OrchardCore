using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;

/// <summary>
/// A lightweight, PII-light summary broadcast to the portal when a new inbound message lands, so an inbox can
/// raise a toast, bump the unread badge, and reorder the conversation list without re-querying the store.
/// </summary>
public sealed class SmsInboundNotification
{
    /// <summary>
    /// Gets or sets the identifier of the conversation the message belongs to.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the DID (service address) that received the message.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact address (E.164) the message came from.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets a short preview of the message body.
    /// </summary>
    public string Preview { get; set; }

    /// <summary>
    /// Gets or sets the unread count on the conversation after this message.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was received.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent the conversation is assigned to, when assigned.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue that owns the conversation, when queue-owned.
    /// </summary>
    public string OwnerQueueId { get; set; }
}

/// <summary>
/// Notifies the portal that a conversation's assignment changed — so a claimed pooled message disappears from
/// the other queue members' inboxes, and an assigned conversation appears in the target agent's inbox.
/// </summary>
public sealed class SmsAssignmentNotification
{
    /// <summary>
    /// Gets or sets the identifier of the conversation whose assignment changed.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent the conversation is now assigned to.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the conversation belongs to, when queue-owned.
    /// </summary>
    public string OwnerQueueId { get; set; }
}

/// <summary>
/// Notifies the portal that an outbound message's delivery state changed (the "Delivered"/"Failed" tick).
/// </summary>
public sealed class SmsDeliveryNotification
{
    /// <summary>
    /// Gets or sets the identifier of the conversation the message belongs to.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the message whose delivery state changed.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the new delivery status.
    /// </summary>
    public SmsDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error code, when the message failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent the conversation is assigned to, when assigned. It selects the
    /// group the receipt is delivered to, so a delivery state never leaves the inbox that owns the thread.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue that owns the conversation, when queue-owned. It selects the
    /// group the receipt is delivered to for a thread no agent has taken yet.
    /// </summary>
    public string OwnerQueueId { get; set; }
}

/// <summary>
/// Announced when a thread misses its first-response target, so a supervisor sees the customer who is waiting
/// before they give up rather than afterwards in a report.
/// </summary>
public sealed class SmsFirstResponseBreachNotification
{
    /// <summary>
    /// Gets or sets the conversation that breached.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the queue that owns the thread.
    /// </summary>
    public string OwnerQueueId { get; set; }

    /// <summary>
    /// Gets or sets the agent the thread was pushed to, when it was pushed to one.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets when the first reply was due.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the breach was detected.
    /// </summary>
    public DateTime BreachedUtc { get; set; }
}
