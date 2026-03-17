using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds an updated transfer notification for the
/// <see cref="ExternalChatRelayEventTypes.WaitTimeUpdated"/> event.
/// </summary>
internal sealed class WaitTimeUpdatedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => "transfer";

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        notification.Id = ChatNotificationSenderExtensions.NotificationIds.Transfer;
        notification.Icon = "fa-solid fa-headset";

        if (!string.IsNullOrEmpty(relayEvent.Content))
        {
            notification.Content = T["Transferring you to a live agent... Estimated wait: {0}.", relayEvent.Content].Value;
            notification.Metadata = new Dictionary<string, string>
            {
                ["estimatedWaitTime"] = relayEvent.Content,
            };
        }
        else
        {
            notification.Content = T["Transferring you to a live agent..."].Value;
        }

        notification.Actions =
        [
            new ChatNotificationAction
            {
                Name = ChatNotificationSenderExtensions.ActionNames.CancelTransfer,
                Label = T["Cancel Transfer"].Value,
                CssClass = "btn-outline-danger",
                Icon = "fa-solid fa-xmark",
            },
        ];

        result.IsUpdate = true;
    }
}
