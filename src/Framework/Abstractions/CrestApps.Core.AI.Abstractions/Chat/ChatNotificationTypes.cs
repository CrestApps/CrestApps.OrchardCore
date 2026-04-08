namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Well-known notification types used by built-in chat notifications. Each type serves as
/// both the unique identifier and the CSS styling class for the notification.
/// </summary>
public static class ChatNotificationTypes
{
    public const string Typing = "typing";
    public const string Transfer = "transfer";
    public const string AgentConnected = "agent-connected";
    public const string AgentReconnecting = "agent-reconnecting";
    public const string ConnectionLost = "connection-lost";
    public const string ConversationEnded = "conversation-ended";
    public const string SessionEnded = "session-ended";
}
