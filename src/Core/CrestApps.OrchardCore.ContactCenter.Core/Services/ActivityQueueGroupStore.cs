using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IActivityQueueGroupStore"/>.
/// </summary>
public sealed class ActivityQueueGroupStore : DocumentCatalog<ActivityQueueGroup, ActivityQueueGroupIndex>, IActivityQueueGroupStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ActivityQueueGroupStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ActivityQueueGroup> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ActivityQueueGroup, ActivityQueueGroupIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
