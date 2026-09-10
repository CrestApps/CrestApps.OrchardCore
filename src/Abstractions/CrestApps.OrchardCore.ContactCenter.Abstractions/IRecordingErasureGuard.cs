namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Guards recording ingest against resurrecting media whose interaction has already had its recording erased.
/// A late ingest that stores media after an erasure request would silently re-create deleted recording bytes,
/// so ingest consults this guard before and after storing and cleans up any media it wrote for an erased
/// interaction.
/// </summary>
public interface IRecordingErasureGuard
{
    /// <summary>
    /// Determines whether the recording for the specified interaction has been erased and therefore must not be
    /// (re-)ingested. A missing interaction is treated as erased, because ingesting media for an interaction that
    /// no longer exists would orphan it.
    /// </summary>
    /// <param name="interactionId">The identifier of the interaction whose recording is being ingested.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording has been erased (or the interaction is gone) and ingest must not proceed; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsRecordingErasedAsync(string interactionId, CancellationToken cancellationToken = default);
}
