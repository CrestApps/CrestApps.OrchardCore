namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the recording state of an interaction.
/// </summary>
public enum RecordingState
{
    /// <summary>
    /// The interaction is not being recorded.
    /// </summary>
    None,

    /// <summary>
    /// The interaction is actively recording.
    /// </summary>
    Recording,

    /// <summary>
    /// Recording is paused (for example during sensitive data capture).
    /// </summary>
    Paused,

    /// <summary>
    /// Recording has stopped.
    /// </summary>
    Stopped,
}
