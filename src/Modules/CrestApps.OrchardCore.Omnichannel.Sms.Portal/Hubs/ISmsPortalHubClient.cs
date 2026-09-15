using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;

/// <summary>
/// The strongly-typed callbacks the SMS portal hub invokes on connected clients (the inbox workspace and the
/// docked messaging widget) to drive toasts, unread badges, list reordering, and live delivery ticks.
/// </summary>
public interface ISmsPortalHubClient
{
    /// <summary>
    /// Notifies the client that a new inbound message landed on a conversation it can see.
    /// </summary>
    /// <param name="notification">The inbound message summary.</param>
    Task NewInboundMessage(SmsInboundNotification notification);

    /// <summary>
    /// Notifies the client that an outbound message's delivery state changed.
    /// </summary>
    /// <param name="notification">The delivery-state change.</param>
    Task MessageDeliveryUpdated(SmsDeliveryNotification notification);

    /// <summary>
    /// Notifies the client that a conversation was claimed or assigned, so a claimed pooled message can
    /// disappear from other inboxes.
    /// </summary>
    /// <param name="notification">The assignment change.</param>
    Task ConversationAssigned(SmsAssignmentNotification notification);

    /// <summary>
    /// Notifies the client that a conversation missed its first-response target, so a supervisor sees the
    /// customer who is waiting before they give up rather than afterwards in a report.
    /// </summary>
    /// <param name="notification">The breach.</param>
    Task FirstResponseBreached(SmsFirstResponseBreachNotification notification);
}
