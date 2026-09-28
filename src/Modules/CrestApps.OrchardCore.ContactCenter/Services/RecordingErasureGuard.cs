using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IRecordingErasureGuard"/> implementation backed by the interaction's durable erasure
/// tombstone. An interaction whose recording was erased carries a stamped erasure instant, and an interaction
/// that no longer exists (deleted by retention) is treated as erased so a late ingest can never resurrect
/// deleted media.
/// </summary>
public sealed class RecordingErasureGuard : IRecordingErasureGuard
{
    private readonly IContactCenterScopeExecutor _scopeExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingErasureGuard"/> class.
    /// </summary>
    /// <param name="scopeExecutor">The scope executor used to read the erasure tombstone from committed state.</param>
    public RecordingErasureGuard(IContactCenterScopeExecutor scopeExecutor)
    {
        _scopeExecutor = scopeExecutor;
    }

    /// <inheritdoc/>
    public async Task<bool> IsRecordingErasedAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return true;
        }

        // The erasure tombstone must be read from committed state rather than the ambient session. The ingest
        // background task processes a sweep of due jobs in a single scope, so an erasure committed by another
        // scope during the download/store window would otherwise be masked by the session identity map. Reading
        // through an isolated child scope guarantees a fresh session that observes the committed tombstone.
        var erased = false;

        await _scopeExecutor.ExecuteAsync<IInteractionManager>(async interactionManager =>
        {
            var interaction = await interactionManager.FindByIdAsync(interactionId, cancellationToken);

            erased = interaction is null || interaction.RecordingErasedUtc is not null;
        });

        return erased;
    }
}
