using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingRealTimeNotifier"/>: does nothing, so the send/receive path can raise events
/// unconditionally even when the SignalR-backed workspace hub is not present. The workspace module replaces this
/// with the real hub notifier.
/// </summary>
public sealed class NullMessagingRealTimeNotifier : IMessagingRealTimeNotifier
{
    /// <inheritdoc/>
    public Task NewInboundMessageAsync(MessagingInboundNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task MessageDeliveryUpdatedAsync(MessagingDeliveryNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ConversationAssignedAsync(MessagingAssignmentNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task FirstResponseBreachedAsync(MessagingFirstResponseBreachNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
