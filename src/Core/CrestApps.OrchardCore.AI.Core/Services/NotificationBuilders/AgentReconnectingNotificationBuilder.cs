using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an agent-reconnecting warning notification for the
/// <see cref="ExternalChatRelayEventTypes.AgentReconnecting"/> event.
/// </summary>
internal sealed class AgentReconnectingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        var content = relayEvent.Content
            ?? (string.IsNullOrEmpty(relayEvent.AgentName)
                ? localizer["Agent is reconnecting..."].Value
                : localizer["{0} is reconnecting...", relayEvent.AgentName].Value);

        return new ExternalChatRelayNotificationResult
        {
            Notification = new ChatNotification
            {
                Id = ChatNotificationSenderExtensions.NotificationIds.AgentReconnecting,
                Type = "warning",
                Content = content,
                Icon = "fa-solid fa-rotate",
            },
        };
    }
}
