using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a removal result for the <see cref="ExternalChatRelayEventTypes.ConnectionRestored"/> event.
/// Removes the connection-lost notification.
/// </summary>
internal sealed class ConnectionRestoredNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        result.RemoveNotificationIds.Add(ChatNotificationSenderExtensions.NotificationIds.ConnectionLost);
        result.Notification = null;
    }
}
