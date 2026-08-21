namespace CrestApps.OrchardCore.Telephony.Sms.Notifications;

/// <summary>
/// Publishes real-time messaging events to the SMS portal (toasts, unread badges, live delivery ticks). The
/// default no-op implementation lets the Core send/receive path raise events unconditionally; the portal
/// module supplies the SignalR-backed implementation.
/// </summary>
public interface ISmsRealTimeNotifier
{
    /// <summary>
    /// Announces a newly received inbound message to the agent(s) responsible for the conversation.
    /// </summary>
    /// <param name="notification">The inbound message summary.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task NewInboundMessageAsync(SmsInboundNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that an outbound message's delivery state changed.
    /// </summary>
    /// <param name="notification">The delivery-state change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task MessageDeliveryUpdatedAsync(SmsDeliveryNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that a conversation's assignment changed (claimed or assigned).
    /// </summary>
    /// <param name="notification">The assignment change.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ConversationAssignedAsync(SmsAssignmentNotification notification, CancellationToken cancellationToken = default);
}
