using CrestApps.Core.AI.Chat;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Describes the notification actions to take when handling an external chat relay event.
/// This is the output from <see cref="IExternalChatRelayNotificationBuilder"/> and is processed
/// by <see cref="IExternalChatRelayNotificationHandler"/>.
/// </summary>
public sealed class ExternalChatRelayNotificationResult
{
    /// <summary>
    /// Gets the notification types to remove before sending the new notification.
    /// For example, an "agent connected" event might remove the "transfer" notification first.
    /// </summary>
    public IList<string> RemoveNotificationTypes { get; } = [];

    /// <summary>
    /// Gets or sets the notification to send, or <see langword="null"/> if the event
    /// only removes existing notifications (e.g., "agent stopped typing" removes the typing indicator).
    /// </summary>
    public ChatNotification Notification { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the notification should be sent as an update
    /// (using <see cref="IChatNotificationSender.UpdateAsync"/>) instead of a new send
    /// (using <see cref="IChatNotificationSender.SendAsync"/>).
    /// When <see langword="true"/>, the notification replaces an existing notification with the same type
    /// only if it exists on the client. When <see langword="false"/> (default), the notification is always sent.
    /// </summary>
    public bool IsUpdate { get; set; }
}
