using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IPredictivePacingStateStore"/>.
/// </summary>
public sealed class PredictivePacingStateStore : DocumentCatalog<PredictivePacingState, PredictivePacingStateIndex>, IPredictivePacingStateStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingStateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public PredictivePacingStateStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The record is the guard that keeps two nodes from both committing a cycle for the same queue, so a write that
    /// raced another is refused rather than silently applied.
    /// </remarks>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public async Task<PredictivePacingState> FindByQueueIdAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        return await Session.Query<PredictivePacingState, PredictivePacingStateIndex>(
            index => index.QueueId == queueId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<PredictivePacingState>> GetByDialerProfileIdAsync(string dialerProfileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(dialerProfileId);

        // One record per over-dialed campaign queue, so the whole set is small; the profile is not indexed.
        var states = await Session.Query<PredictivePacingState, PredictivePacingStateIndex>(collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return states
            .Where(state => string.Equals(state.DialerProfileId, dialerProfileId, StringComparison.Ordinal))
            .OrderByDescending(state => state.LastCycleUtc ?? DateTime.MinValue)
            .ToArray();
    }
}
