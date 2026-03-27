using CrestApps.AI;
using CrestApps.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a typing indicator notification for the <see cref="ExternalChatRelayEventTypes.AgentTyping"/> event.
/// </summary>
internal sealed class AgentTypingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => ChatNotificationTypes.Typing;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Content = string.IsNullOrEmpty(relayEvent.AgentName)
            ? T["Agent is typing"].Value
            : T["{0} is typing", relayEvent.AgentName].Value;
        notification.Icon = "fa-solid fa-ellipsis";
    }
}
