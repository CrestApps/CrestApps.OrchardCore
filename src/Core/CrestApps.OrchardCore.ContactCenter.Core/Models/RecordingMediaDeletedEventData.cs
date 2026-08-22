namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the confirmation details recorded when the underlying recording media has actually been deleted from
/// the owning media store, following an erasure or retention request. This is the durable confirmed-deletion
/// receipt, distinct from the acceptance of the erasure request itself.
/// </summary>
public sealed class RecordingMediaDeletedEventData
{
    /// <summary>
    /// Gets or sets the identifier of the actor that requested the erasure that led to this deletion.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the stated reason for the erasure that led to this deletion.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the opaque recording reference whose media was deleted from the owning media store.
    /// </summary>
    public string RecordingReference { get; set; }
}
