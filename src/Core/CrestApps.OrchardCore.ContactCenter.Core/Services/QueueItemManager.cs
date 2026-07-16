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
