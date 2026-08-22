using CrestApps.OrchardCore.Sms.Workspace.Notifications;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// The default <see cref="ISmsRealTimeNotifier"/>: does nothing, so the send/receive path can raise events
/// unconditionally even when the SignalR-backed portal hub is not present. The portal module replaces this
/// with the real hub notifier.
/// </summary>
public sealed class NullSmsRealTimeNotifier : ISmsRealTimeNotifier
{
    /// <inheritdoc/>
    public Task NewInboundMessageAsync(SmsInboundNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task MessageDeliveryUpdatedAsync(SmsDeliveryNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ConversationAssignedAsync(SmsAssignmentNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
