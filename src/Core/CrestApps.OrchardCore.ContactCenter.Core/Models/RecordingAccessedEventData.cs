namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the audit details recorded when a captured recording is accessed or retrieved.
/// </summary>
public sealed class RecordingAccessedEventData
{
    /// <summary>
    /// Gets or sets the identifier of the actor that accessed the recording.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the stated purpose for accessing the recording.
    /// </summary>
    public string Purpose { get; set; }

    /// <summary>
    /// Gets or sets the opaque recording reference that was accessed.
    /// </summary>
    public string RecordingReference { get; set; }
}
