using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a typing indicator notification for the <see cref="ExternalChatRelayEventTypes.AgentTyping"/> event.
/// </summary>
internal sealed class AgentTypingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        var content = string.IsNullOrEmpty(relayEvent.AgentName)
            ? localizer["Agent is typing"].Value
            : localizer["{0} is typing", relayEvent.AgentName].Value;

        return new ExternalChatRelayNotificationResult
        {
            Notification = new ChatNotification
            {
                Id = ChatNotificationSenderExtensions.NotificationIds.Typing,
                Type = "typing",
                Content = content,
                Icon = "fa-solid fa-ellipsis",
            },
        };
    }
}
