using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterProjectionCheckpointStore"/>.
/// </summary>
public sealed class ContactCenterProjectionCheckpointStore : ConcurrentDocumentCatalog<ContactCenterProjectionCheckpoint, ContactCenterProjectionCheckpointIndex>, IContactCenterProjectionCheckpointStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterProjectionCheckpointStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterProjectionCheckpoint> FindByHandlerAsync(string handlerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerId);

        return await Session.Query<ContactCenterProjectionCheckpoint, ContactCenterProjectionCheckpointIndex>(
            index => index.HandlerId == handlerId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
