using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Services.NotificationBuilders;

/// <summary>
/// Builds a removal result for the <see cref="ExternalChatRelayEventTypes.AgentStoppedTyping"/> event.
/// Removes the typing indicator notification.
/// </summary>
internal sealed class AgentStoppedTypingNotificationBuilder : IExternalChatRelayNotificationBuilder
{
    public ExternalChatRelayNotificationResult Build(ExternalChatRelayEvent relayEvent, IStringLocalizer localizer)
    {
        var result = new ExternalChatRelayNotificationResult();
        result.RemoveNotificationIds.Add(ChatNotificationSenderExtensions.NotificationIds.Typing);

        return result;
    }
}
