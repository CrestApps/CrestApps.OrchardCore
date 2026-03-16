using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an agent-connected notification for the <see cref="ExternalChatRelayEventTypes.AgentConnected"/> event.
/// Removes the transfer notification and sends an agent-connected notification.
/// </summary>
internal sealed class AgentConnectedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        var content = relayEvent.Content
            ?? (string.IsNullOrEmpty(relayEvent.AgentName)
                ? localizer["You are now connected to a live agent."].Value
                : localizer["You are now connected to {0}.", relayEvent.AgentName].Value);

        var result = new ExternalChatRelayNotificationResult
        {
            Notification = new ChatNotification
            {
                Id = ChatNotificationSenderExtensions.NotificationIds.AgentConnected,
                Type = "info",
                Content = content,
                Icon = "fa-solid fa-user-check",
                Dismissible = true,
            },
        };

        result.RemoveNotificationIds.Add(ChatNotificationSenderExtensions.NotificationIds.Transfer);

        return result;
    }
}
