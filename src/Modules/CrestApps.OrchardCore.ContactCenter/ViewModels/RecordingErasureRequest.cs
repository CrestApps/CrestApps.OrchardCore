namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents an authorized right-to-erasure request against a captured recording.
/// </summary>
public sealed class RecordingErasureRequest
{
    /// <summary>
    /// Gets or sets the identifier of the interaction whose recording should be erased.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the stated reason for the erasure request.
    /// </summary>
    public string Reason { get; set; }
}
