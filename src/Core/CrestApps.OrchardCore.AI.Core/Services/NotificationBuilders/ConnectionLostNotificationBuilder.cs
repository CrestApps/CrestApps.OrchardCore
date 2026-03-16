using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a connection-lost error notification for the
/// <see cref="ExternalChatRelayEventTypes.ConnectionLost"/> event.
/// </summary>
internal sealed class ConnectionLostNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        return new ExternalChatRelayNotificationResult
        {
            Notification = new ChatNotification
            {
                Id = ChatNotificationSenderExtensions.NotificationIds.ConnectionLost,
                Type = "error",
                Content = relayEvent.Content ?? localizer["Connection lost. Attempting to reconnect..."].Value,
                Icon = "fa-solid fa-plug-circle-xmark",
            },
        };
    }
}
