using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

/// <summary>
/// Notification transport for the Chat Interaction hub. Sends notification messages
/// to clients connected via <see cref="ChatInteractionHub"/>.
/// </summary>
internal sealed class ChatInteractionNotificationTransport : IChatNotificationTransport
{
    private readonly IHubContext<ChatInteractionHub, IChatInteractionHubClient> _hubContext;

    public ChatInteractionNotificationTransport(IHubContext<ChatInteractionHub, IChatInteractionHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    public Task UpdateNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).UpdateNotification(notification);
    }

    public Task RemoveNotificationAsync(string sessionId, string notificationType)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).RemoveNotification(notificationType);
    }
}
