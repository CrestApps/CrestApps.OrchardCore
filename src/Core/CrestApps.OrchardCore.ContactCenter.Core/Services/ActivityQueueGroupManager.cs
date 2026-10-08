using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityQueueGroupManager"/>.
/// </summary>
public sealed class ActivityQueueGroupManager : CatalogManager<ActivityQueueGroup>, IActivityQueueGroupManager
{
    private readonly IActivityQueueGroupStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupManager"/> class.
    /// </summary>
    /// <param name="store">The underlying queue-group store.</param>
    /// <param name="handlers">The catalog entry handlers for queue groups.</param>
    /// <param name="logger">The logger instance.</param>
    public ActivityQueueGroupManager(
        IActivityQueueGroupStore store,
        IEnumerable<ICatalogEntryHandler<ActivityQueueGroup>> handlers,
        ILogger<CatalogManager<ActivityQueueGroup>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueueGroup> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var group = await _store.FindByNameAsync(name, cancellationToken);

        if (group is not null)
        {
            await LoadAsync(group, cancellationToken);
        }

        return group;
    }
}
