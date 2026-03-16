using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;

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
        public const string AgentConnected = "agent-connected";
        public const string AgentReconnecting = "agent-reconnecting";
        public const string ConnectionLost = "connection-lost";
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
    /// Shows a typing indicator system message (e.g., "Mike is typing…").
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="agentName">Optional name of the agent who is typing.</param>
    public static Task ShowTypingAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string agentName = null)
    {
        var content = string.IsNullOrEmpty(agentName)
            ? T["Agent is typing"].Value
            : T["{0} is typing", agentName].Value;

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
    /// Shows a transfer indicator system message with optional wait time and cancel button.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="message">The transfer message. When <see langword="null"/>, a localized default is used.</param>
    /// <param name="estimatedWaitTime">Optional estimated wait time string (e.g., "2 minutes").</param>
    /// <param name="cancellable">Whether to show a cancel button. Defaults to <see langword="true"/>.</param>
    public static Task ShowTransferAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string message = null,
        string estimatedWaitTime = null,
        bool cancellable = true)
    {
        var content = message ?? T["Transferring you to a live agent..."].Value;

        if (!string.IsNullOrEmpty(estimatedWaitTime))
        {
            content += " " + T["Estimated wait: {0}.", estimatedWaitTime].Value;
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
                    Label = T["Cancel Transfer"].Value,
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
    /// <param name="localizer">The string localizer for translating user-facing messages.</param>
    /// <param name="message">The updated transfer message.</param>
    /// <param name="estimatedWaitTime">Optional updated estimated wait time.</param>
    /// <param name="cancellable">Whether to show a cancel button.</param>
    public static Task UpdateTransferAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer localizer,
        string message = null,
        string estimatedWaitTime = null,
        bool cancellable = true)
    {
        return ShowTransferAsync(sender, sessionId, chatType, localizer, message, estimatedWaitTime, cancellable);
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
    /// Shows an "agent connected" system message indicating a live agent has joined the session.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="agentName">Optional name of the connected agent.</param>
    /// <param name="message">Optional custom message. When <see langword="null"/>, a localized default is used.</param>
    public static Task ShowAgentConnectedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string agentName = null,
        string message = null)
    {
        var content = message
            ?? (string.IsNullOrEmpty(agentName)
                ? T["You are now connected to a live agent."].Value
                : T["You are now connected to {0}.", agentName].Value);

        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.AgentConnected,
            Type = "info",
            Content = content,
            Icon = "fa-solid fa-user-check",
            Dismissible = true,
        });
    }

    /// <summary>
    /// Hides a previously shown agent-connected notification.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    public static Task HideAgentConnectedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType)
    {
        return sender.RemoveAsync(sessionId, chatType, NotificationIds.AgentConnected);
    }

    /// <summary>
    /// Shows an "agent reconnecting" system message indicating the agent is reconnecting after a disruption.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="agentName">Optional name of the reconnecting agent.</param>
    /// <param name="message">Optional custom message. When <see langword="null"/>, a localized default is used.</param>
    public static Task ShowAgentReconnectingAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string agentName = null,
        string message = null)
    {
        var content = message
            ?? (string.IsNullOrEmpty(agentName)
                ? T["Agent is reconnecting..."].Value
                : T["{0} is reconnecting...", agentName].Value);

        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.AgentReconnecting,
            Type = "warning",
            Content = content,
            Icon = "fa-solid fa-rotate",
        });
    }

    /// <summary>
    /// Hides a previously shown agent-reconnecting notification.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    public static Task HideAgentReconnectingAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType)
    {
        return sender.RemoveAsync(sessionId, chatType, NotificationIds.AgentReconnecting);
    }

    /// <summary>
    /// Shows a "connection lost" system message indicating the relay connection has been lost.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="message">Optional custom message. When <see langword="null"/>, a localized default is used.</param>
    public static Task ShowConnectionLostAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string message = null)
    {
        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.ConnectionLost,
            Type = "error",
            Content = message ?? T["Connection lost. Attempting to reconnect..."].Value,
            Icon = "fa-solid fa-plug-circle-xmark",
        });
    }

    /// <summary>
    /// Hides a previously shown connection-lost notification.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    public static Task HideConnectionLostAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType)
    {
        return sender.RemoveAsync(sessionId, chatType, NotificationIds.ConnectionLost);
    }

    /// <summary>
    /// Shows a "conversation ended" system message. The user can still send messages
    /// via text or audio input, but this indicates the conversation mode has ended.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="message">The message to display. When <see langword="null"/>, a localized default is used.</param>
    public static Task ShowConversationEndedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string message = null)
    {
        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.ConversationEnded,
            Type = "ended",
            Content = message ?? T["Conversation ended."].Value,
            Icon = "fa-solid fa-circle-check",
            Dismissible = true,
        });
    }

    /// <summary>
    /// Shows a "session ended" system message and optionally allows ending the session programmatically.
    /// </summary>
    /// <param name="sender">The notification sender.</param>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="T">The string localizer for translating user-facing messages.</param>
    /// <param name="message">The message to display. When <see langword="null"/>, a localized default is used.</param>
    public static Task ShowSessionEndedAsync(
        this IChatNotificationSender sender,
        string sessionId,
        ChatContextType chatType,
        IStringLocalizer T,
        string message = null)
    {
        return sender.SendAsync(sessionId, chatType, new ChatNotification
        {
            Id = NotificationIds.SessionEnded,
            Type = "ended",
            Content = message ?? T["This chat session has ended."].Value,
            Icon = "fa-solid fa-circle-check",
            Dismissible = true,
        });
    }
}
