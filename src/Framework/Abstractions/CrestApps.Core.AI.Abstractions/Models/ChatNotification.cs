using CrestApps.Core.AI.Chat;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents a transient UI notification displayed as a system message in the chat interface.
/// Notifications provide visual feedback to users about system state changes such as
/// typing indicators, agent transfers, or session endings. They are separate from
/// chat history and can be dynamically added, updated, or removed via SignalR.
/// </summary>
public sealed class ChatNotification
{
    /// <summary>
    /// Initializes a new instance of <see cref="ChatNotification"/> with the specified notification type.
    /// </summary>
    /// <param name="type">The notification type, which serves as both the unique identifier
    /// and the CSS styling class. Built-in types are defined in <see cref="ChatNotificationTypes"/>.
    /// Custom types are also supported.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is null or whitespace.</exception>
    public ChatNotification(string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        Type = type;
    }

    /// <summary>
    /// Gets the notification type, which serves as both the unique identifier and the CSS
    /// styling class for the notification. Only one notification of a given type can be
    /// active at a time — sending a new notification with the same type replaces the
    /// existing one. Built-in types are defined in <see cref="ChatNotificationTypes"/>.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    /// Gets or sets the display content of the notification.
    /// Supports plain text. For example: <c>"Mike is typing..."</c> or
    /// <c>"Transferring you to a live agent. Estimated wait: 2 minutes."</c>.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets an optional FontAwesome icon class for the notification.
    /// For example: <c>"fa-solid fa-spinner fa-spin"</c> or <c>"fa-solid fa-headset"</c>.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets an optional CSS class to apply to the notification container.
    /// Use this for custom visual styling beyond the built-in type-based styles.
    /// </summary>
    public string CssClass { get; set; }

    /// <summary>
    /// Gets or sets whether the user can dismiss this notification by clicking a close button.
    /// </summary>
    public bool Dismissible { get; set; }

    /// <summary>
    /// Gets or sets the list of action buttons displayed within the notification.
    /// Actions trigger callbacks to the server via <c>HandleNotificationAction</c>.
    /// </summary>
    public IList<ChatNotificationAction> Actions { get; set; }

    /// <summary>
    /// Gets or sets an extensible metadata dictionary for passing additional
    /// data to the client. For example, estimated wait time or agent name.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
