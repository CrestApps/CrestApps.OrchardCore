namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents an event received from an external chat relay (e.g., a WebSocket connection
/// to a third-party live-agent platform). The event type determines how the framework
/// routes the event to the appropriate notification or message pipeline.
/// </summary>
public sealed class ExternalChatRelayEvent
{
    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    public ExternalChatRelayEventType EventType { get; set; }

    /// <summary>
    /// Gets or sets the text content of the event.
    /// For <see cref="ExternalChatRelayEventType.Message"/>, this is the agent's message text.
    /// For <see cref="ExternalChatRelayEventType.WaitTimeUpdated"/>, this is the estimated wait time string.
    /// For <see cref="ExternalChatRelayEventType.Custom"/>, this is the custom event payload.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the display name of the agent involved in the event.
    /// Used for typing indicators and agent-connected notifications.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the custom event name. Only used when <see cref="EventType"/>
    /// is <see cref="ExternalChatRelayEventType.Custom"/>.
    /// </summary>
    public string CustomEventName { get; set; }

    /// <summary>
    /// Gets or sets an extensible metadata dictionary for passing additional data.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; }
}
