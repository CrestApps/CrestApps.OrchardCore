using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IActivityQueueStore"/>.
/// </summary>
public sealed class ActivityQueueStore : DocumentCatalog<ActivityQueue, ActivityQueueIndex>, IActivityQueueStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ActivityQueueStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueue> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ActivityQueue, ActivityQueueIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityQueue>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var queues = await Session.Query<ActivityQueue, ActivityQueueIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return queues.ToArray();
    }
}
