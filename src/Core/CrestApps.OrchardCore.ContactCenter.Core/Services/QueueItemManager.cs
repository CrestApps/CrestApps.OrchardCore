using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IQueueItemManager"/>.
/// </summary>
public sealed class QueueItemManager : CatalogManager<QueueItem>, IQueueItemManager
{
    private readonly IQueueItemStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemManager"/> class.
    /// </summary>
    /// <param name="store">The underlying queue item store.</param>
    /// <param name="handlers">The catalog entry handlers for queue items.</param>
    /// <param name="logger">The logger instance.</param>
    public QueueItemManager(
        IQueueItemStore store,
        IEnumerable<ICatalogEntryHandler<QueueItem>> handlers,
        ILogger<CatalogManager<QueueItem>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueueItem>> ListWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var items = await _store.ListWaitingAsync(queueId, cancellationToken);

        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }

    /// <inheritdoc/>
    public async Task<QueueItem> FindNextWaitingAsync(
        ActivityQueue queue,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        QueueItem item;

        if (!queue.EnableSlaAging || queue.SlaThresholdSeconds <= 0)
        {
            // Without SLA aging an item's effective priority equals its base priority, so the routing winner
            // is deterministically the first row of the stored routing order (priority descending, then oldest
            // first) — exactly what QueueItemPrioritizer.SelectNext would return. A bounded top-one query
            // therefore yields the same item without materializing the whole waiting backlog.
            item = await _store.FindNextWaitingAsync(queue.ItemId, cancellationToken);
        }
        else
        {
            // SLA aging promotes an older item above a newer higher-priority one by an amount that grows with
            // how long it has waited, so the winner cannot be expressed as a fixed stored order and every
            // candidate must be scored in memory. Aging is opt-in per queue, so this fuller scan is confined
            // to queues that have explicitly requested it.
            var waiting = await _store.ListWaitingAsync(queue.ItemId, cancellationToken);
            item = QueueItemPrioritizer.SelectNext(waiting, queue, utcNow);
        }

        if (item is not null)
        {
            await LoadAsync(item, cancellationToken);
        }

        return item;
    }

    /// <inheritdoc/>
    public async Task<QueueItem> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        var item = await _store.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (item is not null)
        {
            await LoadAsync(item, cancellationToken);
        }

        return item;
    }

    /// <inheritdoc/>
    public Task<int> CountWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        return _store.CountWaitingAsync(queueId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, int>> CountWaitingByQueueIdsAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default)
    {
        return _store.CountWaitingByQueueIdsAsync(queueIds, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<int> CountWaitingOlderThanAsync(
        string queueId,
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default)
    {
        return _store.CountWaitingOlderThanAsync(queueId, thresholdUtc, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<QueueItem> FindLongestWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var item = await _store.FindLongestWaitingAsync(queueId, cancellationToken);

        if (item is not null)
        {
            await LoadAsync(item, cancellationToken);
        }

        return item;
    }
}
