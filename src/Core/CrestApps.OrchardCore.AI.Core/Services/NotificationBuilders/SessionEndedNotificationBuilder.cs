using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a session-ended notification for the <see cref="ExternalChatRelayEventTypes.SessionEnded"/> event.
/// </summary>
internal sealed class SessionEndedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        return new ExternalChatRelayNotificationResult
        {
            Notification = new ChatNotification
            {
                Id = ChatNotificationSenderExtensions.NotificationIds.SessionEnded,
                Type = "ended",
                Content = relayEvent.Content ?? localizer["This chat session has ended."].Value,
                Icon = "fa-solid fa-circle-check",
                Dismissible = true,
            },
        };
    }
}
