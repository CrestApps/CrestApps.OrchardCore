using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterMetricDeltaStore"/>.
/// </summary>
public sealed class ContactCenterMetricDeltaStore : DocumentCatalog<ContactCenterEventMetricDelta, ContactCenterEventMetricDeltaIndex>, IContactCenterMetricDeltaStore
{
    /// <summary>
    /// The maximum number of unfolded contributions a single reader will add to the rolled-up totals.
    /// </summary>
    public const int MaxPendingContributionsPerRead = 10_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMetricDeltaStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterMetricDeltaStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterEventMetricDelta>> ListBatchAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 500 : maxCount;

        // Deliberately unordered. The document query groups by document identity, so the engine cannot satisfy
        // an ordering over the contribution columns from that grouping and instead materializes and sorts every
        // waiting contribution before returning one batch. The roller folds whatever it is handed and deletes
        // exactly those rows, so no order is needed to be correct.
        var deltas = await Session.Query<ContactCenterEventMetricDelta, ContactCenterEventMetricDeltaIndex>(
            collection: ContactCenterStorage.CollectionName)
            .Take(take)
            .ListAsync(cancellationToken);

        return deltas.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterEventMetricDelta>> ListByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        // Bounded on purpose. This runs on the request path, and the cap is the same volume one roller run
        // drains, so a backlog large enough to be truncated here is already a backlog the reader cannot report
        // exactly. Truncating under-reports transiently, which is the behaviour a lagging roller already has.
        var deltas = await Session.Query<ContactCenterEventMetricDelta, ContactCenterEventMetricDeltaIndex>(
            index => index.Date >= fromUtc && index.Date <= toUtc,
            collection: ContactCenterStorage.CollectionName)
            .Take(MaxPendingContributionsPerRead)
            .ListAsync(cancellationToken);

        return deltas.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactCenterMetricContribution>> ListContributionsAfterAsync(long afterDocumentId, int count, CancellationToken cancellationToken = default)
    {
        // Read from the index rather than through the documents. Everything the caller needs is already on the
        // index, and an index query carries no grouping by document identity, so the ordering this walk depends
        // on is answered from the identifier the rows are already keyed by instead of by sorting the table.
        var rows = await Session.QueryIndex<ContactCenterEventMetricDeltaIndex>(
            index => index.DocumentId > afterDocumentId,
            collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.DocumentId)
            .Take(count)
            .ListAsync(cancellationToken);

        return rows
            .Select(row => new ContactCenterMetricContribution(row.DocumentId, row.DateKey, row.EventType, row.Count))
            .ToArray();
    }
}
