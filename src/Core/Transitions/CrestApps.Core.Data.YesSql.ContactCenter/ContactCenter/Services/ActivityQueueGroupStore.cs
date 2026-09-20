using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IActivityQueueGroupStore"/>.
/// </summary>
public sealed class ActivityQueueGroupStore : ConcurrentDocumentCatalog<ActivityQueueGroup, ActivityQueueGroupIndex>, IActivityQueueGroupStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ActivityQueueGroupStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueueGroup> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ActivityQueueGroup, ActivityQueueGroupIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
