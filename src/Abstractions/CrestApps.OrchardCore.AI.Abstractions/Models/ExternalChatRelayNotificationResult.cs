namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Describes the notification actions to take when handling an external chat relay event.
/// This is the output from <see cref="IExternalChatRelayNotificationBuilder"/> and is processed
/// by <see cref="IExternalChatRelayNotificationHandler"/>.
/// </summary>
public sealed class ExternalChatRelayNotificationResult
{
    /// <summary>
    /// Gets the notification IDs to remove before sending the new notification.
    /// For example, an "agent connected" event might remove the "transfer" notification first.
    /// </summary>
    public IList<string> RemoveNotificationIds { get; } = [];

    /// <summary>
    /// Gets or sets the notification to send, or <see langword="null"/> if the event
    /// only removes existing notifications (e.g., "agent stopped typing" removes the typing indicator).
    /// </summary>
    public ChatNotification Notification { get; set; }
}
