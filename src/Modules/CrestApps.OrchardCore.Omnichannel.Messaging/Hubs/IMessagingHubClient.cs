using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;

/// <summary>
/// The strongly-typed callbacks the messaging workspace hub invokes on connected clients (the inbox workspace and the
/// docked messaging widget) to drive toasts, unread badges, list reordering, and live delivery ticks.
/// </summary>
public interface IMessagingHubClient
{
    /// <summary>
    /// Notifies the client that a new inbound message landed on a conversation it can see.
    /// </summary>
    /// <param name="notification">The inbound message summary.</param>
    Task NewInboundMessage(MessagingInboundNotification notification);

    /// <summary>
    /// Notifies the client that an outbound message's delivery state changed.
    /// </summary>
    /// <param name="notification">The delivery-state change.</param>
    Task MessageDeliveryUpdated(MessagingDeliveryNotification notification);

    /// <summary>
    /// Notifies the client that a conversation was claimed or assigned, so a claimed pooled message can
    /// disappear from other inboxes.
    /// </summary>
    /// <param name="notification">The assignment change.</param>
    Task ConversationAssigned(MessagingAssignmentNotification notification);

    /// <summary>
    /// Notifies the client that a conversation missed its first-response target, so a supervisor sees the
    /// customer who is waiting before they give up rather than afterwards in a report.
    /// </summary>
    /// <param name="notification">The breach.</param>
    Task FirstResponseBreached(MessagingFirstResponseBreachNotification notification);
}
