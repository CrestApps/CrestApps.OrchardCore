using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an updated transfer notification for the
/// <see cref="ExternalChatRelayEventTypes.WaitTimeUpdated"/> event.
/// </summary>
internal sealed class WaitTimeUpdatedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        var content = localizer["Transferring you to a live agent..."].Value;

        if (!string.IsNullOrEmpty(relayEvent.Content))
        {
            content += " " + localizer["Estimated wait: {0}.", relayEvent.Content].Value;
        }

        var notification = new ChatNotification
        {
            Id = ChatNotificationSenderExtensions.NotificationIds.Transfer,
            Type = "transfer",
            Content = content,
            Icon = "fa-solid fa-headset",
            Actions =
            [
                new ChatNotificationAction
                {
                    Name = ChatNotificationSenderExtensions.ActionNames.CancelTransfer,
                    Label = localizer["Cancel Transfer"].Value,
                    CssClass = "btn-outline-danger",
                    Icon = "fa-solid fa-xmark",
                },
            ],
        };

        if (!string.IsNullOrEmpty(relayEvent.Content))
        {
            notification.Metadata = new Dictionary<string, string>
            {
                ["estimatedWaitTime"] = relayEvent.Content,
            };
        }

        return new ExternalChatRelayNotificationResult
        {
            Notification = notification,
        };
    }
}
