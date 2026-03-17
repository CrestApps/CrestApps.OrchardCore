using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a connection-lost error notification for the
/// <see cref="ExternalChatRelayEventTypes.ConnectionLost"/> event.
/// </summary>
internal sealed class ConnectionLostNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Id = ChatNotificationSenderExtensions.NotificationIds.ConnectionLost;
        notification.Type = "error";
        notification.Content = relayEvent.Content ?? T["Connection lost. Attempting to reconnect..."].Value;
        notification.Icon = "fa-solid fa-plug-circle-xmark";
    }
}
