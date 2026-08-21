namespace CrestApps.OrchardCore.Telephony.Sms.Models;

/// <summary>
/// Identifies who owns an <c>SmsConversation</c>: a single agent (a personal number) or a queue (a
/// "department" backed by an existing <c>ActivityQueue</c>).
/// </summary>
public enum SmsConversationOwnerType
{
    /// <summary>
    /// The conversation belongs to a single agent's personal inbox.
    /// </summary>
    Personal,

    /// <summary>
    /// The conversation belongs to a queue (a shared department number).
    /// </summary>
    Queue,
}

/// <summary>
/// Describes whether an <c>SmsConversation</c> has been picked up by an agent.
/// </summary>
public enum SmsConversationAssignmentStatus
{
    /// <summary>
    /// No agent owns the conversation yet.
    /// </summary>
    Unassigned,

    /// <summary>
    /// A specific agent is responsible for the conversation.
    /// </summary>
    Assigned,

    /// <summary>
    /// The conversation is visible to every member of the target queue and can be claimed to own.
    /// </summary>
    Pooled,
}

/// <summary>
/// The lifecycle state of an <c>SmsConversation</c>.
/// </summary>
public enum SmsConversationStatus
{
    /// <summary>
    /// The conversation is active in the inbox.
    /// </summary>
    Open,

    /// <summary>
    /// The conversation is temporarily hidden until a later time.
    /// </summary>
    Snoozed,

    /// <summary>
    /// The conversation has been closed.
    /// </summary>
    Closed,

    /// <summary>
    /// The conversation has been marked as spam.
    /// </summary>
    Spam,
}

/// <summary>
/// The kind of target an <c>SmsNumberRoute</c> binds a dialed number to.
/// </summary>
public enum SmsNumberRouteTargetType
{
    /// <summary>
    /// Inbound messages route to a single agent's personal inbox.
    /// </summary>
    Agent,

    /// <summary>
    /// Inbound messages route to a queue (a department).
    /// </summary>
    Queue,
}

/// <summary>
/// How inbound messages for a queue-targeted <c>SmsNumberRoute</c> are distributed.
/// </summary>
public enum SmsNumberRouteDistributionMode
{
    /// <summary>
    /// The conversation is assigned to an agent through the existing reservation/routing strategies.
    /// </summary>
    Routed,

    /// <summary>
    /// The conversation is visible to every queue member and is claimed to own.
    /// </summary>
    SharedPool,
}

/// <summary>
/// The normalized delivery state of an outbound message, mapped from each provider's delivery receipts.
/// </summary>
public enum SmsDeliveryStatus
{
    /// <summary>
    /// The provider has accepted the message but has not yet reported progress. Also the resting state for
    /// inbound and automated messages, which carry no outbound delivery lifecycle.
    /// </summary>
    Queued,

    /// <summary>
    /// The provider has sent the message toward the carrier.
    /// </summary>
    Sent,

    /// <summary>
    /// The carrier confirmed delivery to the handset.
    /// </summary>
    Delivered,

    /// <summary>
    /// The provider or carrier reported a hard failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The message could not be delivered (for example, an unreachable handset).
    /// </summary>
    Undelivered,
}
