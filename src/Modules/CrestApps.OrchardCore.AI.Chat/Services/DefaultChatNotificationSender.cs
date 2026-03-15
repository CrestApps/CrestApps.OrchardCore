using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Default implementation of <see cref="IChatNotificationSender"/> that sends
/// notifications to chat clients via SignalR hub contexts.
/// </summary>
internal sealed class DefaultChatNotificationSender : IChatNotificationSender
{
    private readonly IHubContext<AIChatHub, IAIChatHubClient> _chatHubContext;
    private readonly IHubContext<ChatInteractionHub, IChatInteractionHubClient> _interactionHubContext;

    public DefaultChatNotificationSender(
        IHubContext<AIChatHub, IAIChatHubClient> chatHubContext,
        IHubContext<ChatInteractionHub, IChatInteractionHubClient> interactionHubContext)
    {
        _chatHubContext = chatHubContext;
        _interactionHubContext = interactionHubContext;
    }

    public async Task SendAsync(string sessionId, ChatContextType chatType, ChatNotification notification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(notification);

        var client = GetGroupClient(sessionId, chatType);

        await client.ReceiveNotification(notification);
    }

    public async Task UpdateAsync(string sessionId, ChatContextType chatType, ChatNotification notification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(notification);

        var client = GetGroupClient(sessionId, chatType);

        await client.UpdateNotification(notification);
    }

    public async Task RemoveAsync(string sessionId, ChatContextType chatType, string notificationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);

        var client = GetGroupClient(sessionId, chatType);

        await client.RemoveNotification(notificationId);
    }

    private IChatHubClient GetGroupClient(string sessionId, ChatContextType chatType)
    {
        if (chatType == ChatContextType.AIChatSession)
        {
            var groupName = AIChatHub.GetSessionGroupName(sessionId);
            return _chatHubContext.Clients.Group(groupName);
        }

        var interactionGroupName = ChatInteractionHub.GetInteractionGroupName(sessionId);
        return _interactionHubContext.Clients.Group(interactionGroupName);
    }
}
