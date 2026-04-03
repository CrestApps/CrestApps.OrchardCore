using CrestApps.AI.Chat;
using CrestApps.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a session-ended notification for the <see cref="ExternalChatRelayEventTypes.SessionEnded"/> event.
/// </summary>
internal sealed class SessionEndedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => ChatNotificationTypes.SessionEnded;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Content = relayEvent.Content ?? T["This chat session has ended."].Value;
        notification.Icon = "fa-solid fa-circle-check";
        notification.Dismissible = true;
    }
}
