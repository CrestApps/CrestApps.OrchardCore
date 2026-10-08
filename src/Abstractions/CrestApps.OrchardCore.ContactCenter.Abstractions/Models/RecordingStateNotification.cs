namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Notifies connected agent and supervisor clients that the recording state of a live interaction changed, so
/// the agent desktop can reflect the pause and the supervisor dashboard can show that a sensitive-data capture is
/// in progress and suppress live monitoring audio for the secured segment.
/// </summary>
public sealed class RecordingStateNotification
{
    /// <summary>
    /// Gets or sets the identifier of the interaction whose recording state changed.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of the agent handling the interaction, when known, so the
    /// notification can be delivered to the agent's own connections.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the agent-profile identifier of the agent handling the interaction, when known.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the recording state of the interaction after the change, expressed as its stable name.
    /// </summary>
    public string RecordingState { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recording is currently paused for a sensitive-data capture. When
    /// <see langword="true"/>, supervisor clients must not present or route live monitoring audio for the segment.
    /// </summary>
    public bool IsSecurePauseActive { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recording state change was broadcast.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
