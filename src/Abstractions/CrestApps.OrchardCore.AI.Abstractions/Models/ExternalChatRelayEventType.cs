namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Identifies the type of event received from an external chat relay.
/// </summary>
public enum ExternalChatRelayEventType
{
    /// <summary>
    /// The external agent is typing a response.
    /// </summary>
    AgentTyping,

    /// <summary>
    /// The external agent has stopped typing.
    /// </summary>
    AgentStoppedTyping,

    /// <summary>
    /// A live agent has connected to the session.
    /// </summary>
    AgentConnected,

    /// <summary>
    /// A live agent has disconnected from the session.
    /// </summary>
    AgentDisconnected,

    /// <summary>
    /// The external system has sent a chat message.
    /// </summary>
    Message,

    /// <summary>
    /// The estimated wait time has been updated.
    /// </summary>
    WaitTimeUpdated,

    /// <summary>
    /// The external session has ended.
    /// </summary>
    SessionEnded,

    /// <summary>
    /// A custom event type defined by the external system.
    /// </summary>
    Custom,
}
