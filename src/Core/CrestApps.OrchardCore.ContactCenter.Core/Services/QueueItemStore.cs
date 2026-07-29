using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Dapper;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IQueueItemStore"/>.
/// </summary>
public sealed class QueueItemStore : DocumentCatalog<QueueItem, QueueItemIndex>, IQueueItemStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    private const int QueryBatchSize = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public QueueItemStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueueItem>> ListWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var items = await Session.Query<QueueItem, QueueItemIndex>(
            index => index.QueueId == queueId && index.Status == QueueItemStatus.Waiting,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.Priority)
            .ThenBy(index => index.EnqueuedUtc)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    /// <inheritdoc/>
    public async Task<QueueItem> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        return await Session.Query<QueueItem, QueueItemIndex>(
            index => index.ActivityItemId == activityItemId,
            collection: ContactCenterConstants.CollectionName)
            .OrderByDescending(index => index.EnqueuedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        return await Session.Query<QueueItem, QueueItemIndex>(
            index => index.QueueId == queueId && index.Status == QueueItemStatus.Waiting,
            collection: ContactCenterConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, int>> CountWaitingByQueueIdsAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueIds);

        if (queueIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        var configuration = Session.Store.Configuration;

        // Counting every queue in one statement rather than one statement per queue: the agent workspace polls
        // this for every queue an agent belongs to, so a query per queue makes the cost of a single poll grow
        // with how many queues the agent covers, and it is the agents covering the most queues whose polls
        // must stay cheapest. The flush is what makes the raw statement safe: it runs on the session's own
        // transaction, so it must first see the writes the caller has made in this unit of work but not yet
        // committed.
        await Session.FlushAsync(cancellationToken);
        var transaction = await Session.BeginTransactionAsync(cancellationToken);

        foreach (var queueIdBatch in queueIds.Chunk(QueryBatchSize))
        {
            var sql = QueueItemQueries.BuildWaitingCountByQueueSql(configuration, queueIdBatch.Length);
            var parameters = new DynamicParameters();

            for (var index = 0; index < queueIdBatch.Length; index++)
            {
                parameters.Add(QueueItemQueries.QueueIdParameterName(index), queueIdBatch[index]);
            }

            var rows = await transaction.Connection.QueryAsync<QueueWaitingCount>(
                new CommandDefinition(
                    sql,
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var row in rows)
            {
                counts[row.QueueId] = row.WaitingCount;
            }
        }

        return counts;
    }

    /// <inheritdoc/>
    public async Task<int> CountWaitingOlderThanAsync(
        string queueId,
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        return await Session.Query<QueueItem, QueueItemIndex>(
            index => index.QueueId == queueId
                && index.Status == QueueItemStatus.Waiting
                && index.EnqueuedUtc < thresholdUtc,
            collection: ContactCenterConstants.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<QueueItem> FindLongestWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        return await Session.Query<QueueItem, QueueItemIndex>(
            index => index.QueueId == queueId && index.Status == QueueItemStatus.Waiting,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.EnqueuedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed class QueueWaitingCount
    {
        public string QueueId { get; set; }

        public int WaitingCount { get; set; }
    }
}
