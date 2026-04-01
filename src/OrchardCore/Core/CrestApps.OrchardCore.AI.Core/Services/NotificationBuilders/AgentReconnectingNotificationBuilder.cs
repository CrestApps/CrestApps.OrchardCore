using CrestApps.AI.Chat;
using CrestApps.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an agent-reconnecting warning notification for the
/// <see cref="ExternalChatRelayEventTypes.AgentReconnecting"/> event.
/// </summary>
internal sealed class AgentReconnectingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => ChatNotificationTypes.AgentReconnecting;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Content = relayEvent.Content
        ?? (string.IsNullOrEmpty(relayEvent.AgentName)
        ? T["Agent is reconnecting..."].Value
        : T["{0} is reconnecting...", relayEvent.AgentName].Value);
        notification.Icon = "fa-solid fa-rotate";
    }
}
