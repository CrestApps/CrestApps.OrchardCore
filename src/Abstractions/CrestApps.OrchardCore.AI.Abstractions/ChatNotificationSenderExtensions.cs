using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides convenient extension methods for <see cref="IChatNotificationSender"/>
/// to send common notification types without manually constructing <see cref="ChatNotification"/> objects.
/// </summary>
public static class ChatNotificationSenderExtensions
{
    /// <summary>
    /// Well-known notification IDs used by built-in notification types.
    /// </summary>
    public static class NotificationIds
    {
        public const string Typing = "typing";
        public const string Transfer = "transfer";
        public const string ConversationEnded = "conversation-ended";
        public const string SessionEnded = "session-ended";
    }

    /// <summary>
    /// Well-known notification action names.
    /// </summary>
    public static class ActionNames
    {
        public const string CancelTransfer = "cancel-transfer";
        public const string EndSession = "end-session";
    }

    /// <summary>
    /// Shows a typing indicator bubble (e.g., "Mike is typing...").
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="agentName">Optional name of the agent who is typing.</param>
    public static Task ShowTypingAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        string agentName = null)
    {
        var content = string.IsNullOrEmpty(agentName)
            ? "Agent is typing"
            : $"{agentName} is typing";

        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.Typing,
            Type = "typing",
            Content = content,
            Icon = "fa-solid fa-ellipsis",
        });
    }

    /// <summary>
    /// Hides a previously shown typing indicator.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    public static Task HideTypingAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType)
    {
        return sender.RemoveAsync(sessionId, chatType, NotificationIds.Typing);
    }

    /// <summary>
    /// Shows a transfer indicator bubble with optional wait time and cancel button.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="message">The transfer message. Defaults to "Transferring you to a live agent...".</param>
    /// <param name="estimatedWaitTime">Optional estimated wait time string (e.g., "2 minutes").</param>
    /// <param name="cancellable">Whether to show a cancel button. Defaults to <see langword="true"/>.</param>
    public static Task ShowTransferAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        string message = null,
        string estimatedWaitTime = null,
        bool cancellable = true)
    {
        var content = message ?? "Transferring you to a live agent...";

        if (!string.IsNullOrEmpty(estimatedWaitTime))
        {
            content += $" Estimated wait: {estimatedWaitTime}.";
        }

        var notification = new ChatNotification
        {
            Id = NotificationIds.Transfer,
            Type = "transfer",
            Content = content,
            Icon = "fa-solid fa-headset",
        };

        if (cancellable)
        {
            notification.Actions =
            [
                new ChatNotificationAction
                {
                    Name = ActionNames.CancelTransfer,
                    Label = "Cancel Transfer",
                    CssClass = "btn-outline-danger",
                    Icon = "fa-solid fa-xmark",
                },
            ];
        }

        if (!string.IsNullOrEmpty(estimatedWaitTime))
        {
            notification.Metadata = new Dictionary<string, string>
            {
                ["estimatedWaitTime"] = estimatedWaitTime,
            };
        }

        return sender.SendAsync(sessionId, chatType, notification);
    }

    /// <summary>
    /// Updates the transfer indicator with new information (e.g., updated wait time).
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="message">The updated transfer message.</param>
    /// <param name="estimatedWaitTime">Optional updated estimated wait time.</param>
    /// <param name="cancellable">Whether to show a cancel button.</param>
    public static Task UpdateTransferAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        string message = null,
        string estimatedWaitTime = null,
        bool cancellable = true)
    {
        return ShowTransferAsync(sender, sessionId, chatType, message, estimatedWaitTime, cancellable);
    }

    /// <summary>
    /// Hides a previously shown transfer indicator.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    public static Task HideTransferAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType)
    {
        return sender.RemoveAsync(sessionId, chatType, NotificationIds.Transfer);
    }

    /// <summary>
    /// Shows a "conversation ended" bubble. The user can still send messages
    /// via text or audio input, but this indicates the conversation mode has ended.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="message">The message to display. Defaults to "Conversation ended.".</param>
    public static Task ShowConversationEndedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        string message = null)
    {
        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.ConversationEnded,
            Type = "ended",
            Content = message ?? "Conversation ended.",
            Icon = "fa-solid fa-circle-check",
            Dismissible = true,
        });
    }

    /// <summary>
    /// Shows a "session ended" bubble and optionally allows ending the session programmatically.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="message">The message to display. Defaults to "This chat session has ended.".</param>
    public static Task ShowSessionEndedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        string message = null)
    {
        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.SessionEnded,
            Type = "ended",
            Content = message ?? "This chat session has ended.",
            Icon = "fa-solid fa-circle-check",
            Dismissible = true,
        });
    }
}
