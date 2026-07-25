using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Governs post-capture access to recordings. It records the recording access audit trail and enforces the
/// right-to-erasure policy at the orchestration layer, delegating actual media deletion to the owning media store.
/// </summary>
public interface IRecordingAccessGovernanceService
{
    /// <summary>
    /// Records that a captured recording was accessed or retrieved, appending an entry to the recording access
    /// audit trail.
    /// </summary>
    /// <param name="interactionId">The identifier of the interaction whose recording was accessed.</param>
    /// <param name="actorId">The identifier of the actor that accessed the recording.</param>
    /// <param name="purpose">The stated purpose for accessing the recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the access was audited; otherwise, <see langword="false"/> when the interaction has no captured recording to access.</returns>
    Task<bool> RecordAccessAsync(
        string interactionId,
        string actorId,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases the captured recording reference for an interaction in response to a right-to-erasure request,
    /// unless the recording is under legal hold. Actual media deletion is delegated to the owning media store
    /// through the published erasure event.
    /// </summary>
    /// <param name="interactionId">The identifier of the interaction whose recording should be erased.</param>
    /// <param name="actorId">The identifier of the actor that requested erasure.</param>
    /// <param name="reason">The stated reason for the erasure request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A decision describing whether the recording reference was erased or why erasure was denied.</returns>
    Task<RecordingErasureDecision> EraseAsync(
        string interactionId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default);
}
