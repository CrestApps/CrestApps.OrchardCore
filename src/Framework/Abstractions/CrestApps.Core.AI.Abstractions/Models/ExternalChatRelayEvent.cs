namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents an event received from an external chat relay (e.g., a WebSocket connection
/// to a third-party live-agent platform). The event type determines how the framework
/// routes the event to the appropriate notification or message pipeline.
/// </summary>
public sealed class ExternalChatRelayEvent
{
    /// <summary>
    /// Gets or sets the type of the event.
    /// Use constants from <see cref="ExternalChatRelayEventTypes"/> for well-known types,
    /// or any custom string for platform-specific event types.
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// Gets or sets the text content of the event.
    /// For <see cref="ExternalChatRelayEventTypes.Message"/>, this is the agent's message text.
    /// For <see cref="ExternalChatRelayEventTypes.WaitTimeUpdated"/>, this is the estimated wait time string.
    /// For custom event types, this is the event payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the display name of the agent involved in the event.
    /// Used for typing indicators and agent-connected notifications.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets an extensible metadata dictionary for passing additional data.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
