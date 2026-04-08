using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Sends transient UI notifications to chat clients via SignalR.
/// Notifications appear as system messages in the chat interface and are separate
/// from chat history. Use this service from webhooks, background tasks, or response
/// handlers to provide real-time feedback to users.
/// </summary>
/// <remarks>
/// <para>Notifications are sent to SignalR groups, so all clients connected to the
/// same session receive the notification. The group name is determined by the
/// <paramref name="chatType"/> parameter.</para>
/// </remarks>
public interface IChatNotificationSender
{
    /// <summary>
    /// Sends a notification to all clients connected to the specified session.
    /// If a notification with the same <see cref="ChatNotification.Type"/> already exists
    /// on the client, it will be replaced.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="notification">The notification to display.</param>
    Task SendAsync(string sessionId, ChatContextType chatType, ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on all connected clients.
    /// Only replaces the notification if one with a matching <see cref="ChatNotification.Type"/> exists.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="notification">The updated notification.</param>
    Task UpdateAsync(string sessionId, ChatContextType chatType, ChatNotification notification);

    /// <summary>
    /// Removes a notification from all connected clients.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="notificationType">The type of the notification to remove.</param>
    Task RemoveAsync(string sessionId, ChatContextType chatType, string notificationType);
}
