using CrestApps.Core.AI.Chat;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Provides well-known event type constants for <see cref="ExternalChatRelayEvent.EventType"/>.
/// These values are used by the default <see cref="IExternalChatRelayEventHandler"/> to route
/// events to the appropriate notification or message pipeline.
/// </summary>
/// <remarks>
/// Event types are strings rather than an enum so that third-party integrations can define
/// custom event types without modifying the framework. The default handler ignores any
/// event type it does not recognize and logs a debug message.
/// </remarks>
public static class ExternalChatRelayEventTypes
{
    /// <summary>
    /// The external agent is typing a response.
    /// </summary>
    public const string AgentTyping = "agent-typing";

    /// <summary>
    /// The external agent has stopped typing.
    /// </summary>
    public const string AgentStoppedTyping = "agent-stopped-typing";

    /// <summary>
    /// A live agent has connected to the session.
    /// </summary>
    public const string AgentConnected = "agent-connected";

    /// <summary>
    /// A live agent has disconnected from the session.
    /// </summary>
    public const string AgentDisconnected = "agent-disconnected";

    /// <summary>
    /// The external agent is reconnecting to the session after a disruption.
    /// </summary>
    public const string AgentReconnecting = "agent-reconnecting";

    /// <summary>
    /// The connection to the external system has been lost.
    /// </summary>
    public const string ConnectionLost = "connection-lost";

    /// <summary>
    /// The connection to the external system has been restored after a loss.
    /// </summary>
    public const string ConnectionRestored = "connection-restored";

    /// <summary>
    /// The external system has sent a chat message.
    /// </summary>
    public const string Message = "message";

    /// <summary>
    /// The estimated wait time has been updated.
    /// </summary>
    public const string WaitTimeUpdated = "wait-time-updated";

    /// <summary>
    /// The external session has ended.
    /// </summary>
    public const string SessionEnded = "session-ended";
}
