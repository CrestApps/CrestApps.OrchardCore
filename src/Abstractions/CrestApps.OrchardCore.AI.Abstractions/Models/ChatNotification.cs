namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a transient UI notification displayed as a system message in the chat interface.
/// Notifications provide visual feedback to users about system state changes such as
/// typing indicators, agent transfers, or session endings. They are separate from
/// chat history and can be dynamically added, updated, or removed via SignalR.
/// </summary>
public class ChatNotification
{
    /// <summary>
    /// Gets or sets the unique identifier for this notification.
    /// Used to target specific notifications for update or removal.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the notification type, which determines visual styling.
    /// Built-in types: <c>"typing"</c>, <c>"transfer"</c>, <c>"ended"</c>,
    /// <c>"info"</c>, <c>"warning"</c>. Custom types are also supported.
    /// </summary>
    public string Type { get; set; }

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
