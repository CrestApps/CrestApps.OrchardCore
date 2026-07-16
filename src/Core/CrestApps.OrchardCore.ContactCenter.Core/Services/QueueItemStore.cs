using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IQueueItemStore"/>.
/// </summary>
public sealed class QueueItemStore : DocumentCatalog<QueueItem, QueueItemIndex>, IQueueItemStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

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
}
