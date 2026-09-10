namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the audit details recorded when a captured recording reference is erased at the orchestration layer in
/// response to a right-to-erasure request.
/// </summary>
public sealed class RecordingErasedEventData
{
    /// <summary>
    /// Gets or sets the identifier of the actor that requested erasure.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the stated reason for the erasure request.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the opaque recording reference that was erased, allowing the owning media store to delete the
    /// underlying media.
    /// </summary>
    public string RecordingReference { get; set; }
}
