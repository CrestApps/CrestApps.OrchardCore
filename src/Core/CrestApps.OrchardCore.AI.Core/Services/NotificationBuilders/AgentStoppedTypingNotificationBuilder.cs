using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a removal result for the <see cref="ExternalChatRelayEventTypes.AgentStoppedTyping"/> event.
/// Removes the typing indicator notification.
/// </summary>
internal sealed class AgentStoppedTypingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public string NotificationType => null;

    public void Build(ExternalChatRelayEvent relayEvent, ChatNotification notification, ExternalChatRelayNotificationResult result, IStringLocalizer T)
    {
        result.RemoveNotificationTypes.Add(ChatNotificationTypes.Typing);
        result.Notification = null;
    }
}
