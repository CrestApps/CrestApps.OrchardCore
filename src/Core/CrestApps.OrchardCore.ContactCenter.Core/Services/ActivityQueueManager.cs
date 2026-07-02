using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityQueueManager"/>.
/// </summary>
public sealed class ActivityQueueManager : CatalogManager<ActivityQueue>, IActivityQueueManager
{
    private readonly IActivityQueueStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueManager"/> class.
    /// </summary>
    /// <param name="store">The underlying queue store.</param>
    /// <param name="handlers">The catalog entry handlers for queues.</param>
    /// <param name="logger">The logger instance.</param>
    public ActivityQueueManager(
        IActivityQueueStore store,
        IEnumerable<ICatalogEntryHandler<ActivityQueue>> handlers,
        ILogger<CatalogManager<ActivityQueue>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueue> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var queue = await _store.FindByNameAsync(name, cancellationToken);

        if (queue is not null)
        {
            await LoadAsync(queue, cancellationToken);
        }

        return queue;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityQueue>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var queues = await _store.ListEnabledAsync(cancellationToken);

        foreach (var queue in queues)
        {
            await LoadAsync(queue, cancellationToken);
        }

        return queues;
    }
}
