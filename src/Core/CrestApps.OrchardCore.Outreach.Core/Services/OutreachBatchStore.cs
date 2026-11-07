using CrestApps.OrchardCore.Outreach.Core.Indexes;
using CrestApps.OrchardCore.Outreach.Core.Models;
using CrestApps.OrchardCore.WorkQueues;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Stores outreach batches in YesSql, with the concurrency check the leased work queue relies on.
/// </summary>
public sealed class OutreachBatchStore : DocumentCatalog<OutreachBatch, OutreachBatchIndex>, IOutreachBatchStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachBatchStore"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public OutreachBatchStore(ISession session)
        : base(session)
    {
        CollectionName = OutreachConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<OutreachBatch>> GetDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 25 : maxCount;

        var due = await Session.Query<OutreachBatch, OutreachBatchIndex>(
            index => (index.Status == LeasedWorkItemStatus.Pending || index.Status == LeasedWorkItemStatus.Claimed) &&
                index.NextAttemptUtc <= nowUtc,
            collection: OutreachConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return due.ToArray();
    }
}
