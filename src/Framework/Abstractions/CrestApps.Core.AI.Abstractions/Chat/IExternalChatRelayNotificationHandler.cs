using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Handles sending and removing chat notifications described by an
/// <see cref="ExternalChatRelayNotificationResult"/>. This is the "handler" half of
/// the builder/handler pattern used by the relay event system.
/// </summary>
/// <remarks>
/// The default implementation removes all notifications listed in
/// <see cref="ExternalChatRelayNotificationResult.RemoveNotificationTypes"/> and then sends the
/// <see cref="ExternalChatRelayNotificationResult.Notification"/> (if present) via
/// <see cref="IChatNotificationSender"/>.
/// </remarks>
public interface IExternalChatRelayNotificationHandler
{
    /// <summary>
    /// Processes a notification result by removing and/or sending notifications.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="result">The builder result describing which notifications to remove/send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task HandleAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayNotificationResult result,
        CancellationToken cancellationToken = default);
}
