using CrestApps.Core.AI.Chat;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents an action button within a <see cref="ChatNotification"/>.
/// When clicked, the action triggers a callback to the server via the
/// <c>HandleNotificationAction</c> hub method, which dispatches to
/// registered <see cref="IChatNotificationActionHandler"/> implementations.
/// </summary>
public class ChatNotificationAction
{
    /// <summary>
    /// Gets or sets the unique action identifier.
    /// This value is sent to the server when the user clicks the button
    /// and is used to resolve the appropriate <see cref="IChatNotificationActionHandler"/>.
    /// Built-in actions: <c>"cancel-transfer"</c>, <c>"end-session"</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display label for the action button.
    /// For example: <c>"Cancel Transfer"</c> or <c>"End Chat"</c>.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the CSS class for the action button.
    /// For example: <c>"btn-outline-danger"</c> or <c>"btn-outline-secondary"</c>.
    /// Defaults to <c>"btn-outline-secondary"</c> on the client if not specified.
    /// </summary>
    public string CssClass { get; set; }

    /// <summary>
    /// Gets or sets an optional FontAwesome icon class for the button.
    /// For example: <c>"fa-solid fa-xmark"</c>.
    /// </summary>
    public string Icon { get; set; }
}
