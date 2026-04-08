using CrestApps.Core.AI.Chat;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Provides context to an <see cref="IChatNotificationActionHandler"/> when the user
/// clicks an action button on a chat notification system message.
/// </summary>
public sealed class ChatNotificationActionContext
{
    /// <summary>
    /// Gets the session or interaction identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the type of the notification that contains the action.
    /// </summary>
    public required string NotificationType { get; init; }

    /// <summary>
    /// Gets the name of the action that was triggered.
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// Gets the type of chat context (AI Chat Session or Chat Interaction).
    /// </summary>
    public required ChatContextType ChatType { get; init; }

    /// <summary>
    /// Gets the SignalR connection ID of the client that triggered the action.
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// Gets the scoped service provider for resolving dependencies.
    /// </summary>
    public required IServiceProvider Services { get; init; }
}
