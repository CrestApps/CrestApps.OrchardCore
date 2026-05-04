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

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionNotificationTransport"/> class.
    /// </summary>
    /// <param name="hubContext">The hub context used to send messages to connected chat interaction clients.</param>
    public ChatInteractionNotificationTransport(IHubContext<ChatInteractionHub, IChatInteractionHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task SendNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).ReceiveNotification(notification);
    }

    /// <inheritdoc />
    public Task UpdateNotificationAsync(string sessionId, ChatNotification notification)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).UpdateNotification(notification);
    }

    /// <inheritdoc />
    public Task RemoveNotificationAsync(string sessionId, string notificationType)
    {
        var groupName = ChatInteractionHub.GetInteractionGroupName(sessionId);

        return _hubContext.Clients.Group(groupName).RemoveNotification(notificationType);
    }
}
