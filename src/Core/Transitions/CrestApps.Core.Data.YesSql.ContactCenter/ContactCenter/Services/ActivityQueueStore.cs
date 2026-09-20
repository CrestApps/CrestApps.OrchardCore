using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IActivityQueueStore"/>.
/// </summary>
public sealed class ActivityQueueStore : ConcurrentDocumentCatalog<ActivityQueue, ActivityQueueIndex>, IActivityQueueStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ActivityQueueStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueue> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ActivityQueue, ActivityQueueIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ActivityQueue>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var queues = await Session.Query<ActivityQueue, ActivityQueueIndex>(
            index => index.Enabled,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return queues.ToArray();
    }
}
