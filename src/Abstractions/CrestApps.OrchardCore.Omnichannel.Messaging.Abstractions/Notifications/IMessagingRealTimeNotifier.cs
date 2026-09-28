namespace CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;

/// <summary>
/// Publishes real-time messaging events to the messaging workspace (toasts, unread badges, live delivery ticks). The
/// default no-op implementation lets the Core send/receive path raise events unconditionally; the workspace
/// module supplies the SignalR-backed implementation.
/// </summary>
public interface IMessagingRealTimeNotifier
{
    /// <summary>
    /// Announces a newly received inbound message to the agent(s) responsible for the conversation.
    /// </summary>
    /// <param name="notification">The inbound message summary.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NewInboundMessageAsync(MessagingInboundNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that an outbound message's delivery state changed.
    /// </summary>
    /// <param name="notification">The delivery-state change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task MessageDeliveryUpdatedAsync(MessagingDeliveryNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that a conversation's assignment changed (claimed or assigned).
    /// </summary>
    /// <param name="notification">The assignment change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ConversationAssignedAsync(MessagingAssignmentNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that a conversation missed its first-response target.
    /// </summary>
    /// <param name="notification">The breach notification.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task FirstResponseBreachedAsync(MessagingFirstResponseBreachNotification notification, CancellationToken cancellationToken = default);
}
