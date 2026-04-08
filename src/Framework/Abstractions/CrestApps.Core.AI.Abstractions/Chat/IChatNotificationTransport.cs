using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Defines the low-level transport for delivering chat notification messages to
/// clients connected to a specific hub. Implementations are registered as keyed
/// services using <see cref="ChatContextType"/> as the key, enabling the
/// <see cref="IChatNotificationSender"/> to dispatch notifications without
/// coupling to concrete hub types.
/// </summary>
/// <remarks>
/// <para>
/// Each hub that supports notification system messages should provide its own implementation
/// and register it as a keyed service:
/// </para>
/// <code>
/// services.AddKeyedScoped&lt;IChatNotificationTransport, MyChatNotificationTransport&gt;(ChatContextType.AIChatSession);
/// </code>
/// </remarks>
public interface IChatNotificationTransport
{
    /// <summary>
    /// Sends a notification to all clients in the session group. If a notification
    /// with the same <see cref="ChatNotification.Type"/> already exists on the client,
    /// it is replaced.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="notification">The notification to display.</param>
    Task SendNotificationAsync(string sessionId, ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on all connected clients in the session group.
    /// Only replaces the notification if one with a matching <see cref="ChatNotification.Type"/> exists.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="notification">The updated notification.</param>
    Task UpdateNotificationAsync(string sessionId, ChatNotification notification);

    /// <summary>
    /// Removes a notification from all connected clients in the session group.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="notificationType">The type of the notification to remove.</param>
    Task RemoveNotificationAsync(string sessionId, string notificationType);
}
