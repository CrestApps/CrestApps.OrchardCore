using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a removal result for the <see cref="ExternalChatRelayEventTypes.AgentDisconnected"/> event.
/// Removes the agent-connected notification.
/// </summary>
internal sealed class AgentDisconnectedNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => null;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        result.RemoveNotificationIds.Add(ChatNotificationSenderExtensions.NotificationIds.AgentConnected);
        result.Notification = null;
    }
}
