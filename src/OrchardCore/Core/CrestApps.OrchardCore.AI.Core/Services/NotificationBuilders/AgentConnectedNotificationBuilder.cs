using CrestApps.AI.Chat;
using CrestApps.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an agent-connected notification for the <see cref="ExternalChatRelayEventTypes.AgentConnected"/> event.
/// Removes the transfer notification and sends an agent-connected notification.
/// </summary>
internal sealed class AgentConnectedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => ChatNotificationTypes.AgentConnected;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Content = relayEvent.Content
        ?? (string.IsNullOrEmpty(relayEvent.AgentName)
        ? T["You are now connected to a live agent."].Value
        : T["You are now connected to {0}.", relayEvent.AgentName].Value);
        notification.Icon = "fa-solid fa-user-check";
        notification.Dismissible = true;

        result.RemoveNotificationTypes.Add(ChatNotificationTypes.Transfer);
    }
}
