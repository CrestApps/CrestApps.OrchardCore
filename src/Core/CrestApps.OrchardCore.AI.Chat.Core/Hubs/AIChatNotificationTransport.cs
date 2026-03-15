using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.AI.Chat.Core.Hubs;

/// <summary>
/// Notification transport for the AI Chat hub. Sends notification messages
/// to clients connected via <see cref="AIChatHub"/>.
/// </summary>
internal sealed class AIChatNotificationTransport : IChatNotificationTransport
{
    private readonly IHubContext<AIChatHub, IAIChatHubClient> _hubContext;

    public AIChatNotificationTransport(IHubContext<AIChatHub, IAIChatHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = AIChatHub.GetSessionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    public Task UpdateNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = AIChatHub.GetSessionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).UpdateNotification(notification);
    }

    public Task RemoveNotificationAsync(string sessionId, string notificationId)
    {
        var groupName = AIChatHub.GetSessionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).RemoveNotification(notificationId);
    }
}
