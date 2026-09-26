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
    private readonly IContactCenterConfigurationCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueManager"/> class.
    /// </summary>
    /// <param name="store">The underlying queue store.</param>
    /// <param name="handlers">The catalog entry handlers for queues.</param>
    /// <param name="cache">The routing configuration cache used to serve enabled queues without re-querying the store.</param>
    /// <param name="logger">The logger instance.</param>
    public ActivityQueueManager(
        IActivityQueueStore store,
        IEnumerable<ICatalogEntryHandler<ActivityQueue>> handlers,
        IContactCenterConfigurationCache cache,
        ILogger<CatalogManager<ActivityQueue>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _cache = cache;
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
    public async Task<IReadOnlyCollection<ActivityQueue>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetEnabledAsync(async token =>
        {
            var queues = await _store.GetEnabledAsync(token);

            foreach (var queue in queues)
            {
                await LoadAsync(queue, token);
            }

            return queues;
        }, cancellationToken);
    }
}
