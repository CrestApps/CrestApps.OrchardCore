using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterProjectionCheckpointStore"/>.
/// </summary>
public sealed class ContactCenterProjectionCheckpointStore : DocumentCatalog<ContactCenterProjectionCheckpoint, ContactCenterProjectionCheckpointIndex>, IContactCenterProjectionCheckpointStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterProjectionCheckpointStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterProjectionCheckpoint> FindByHandlerAsync(string handlerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(handlerId);

        return await Session.Query<ContactCenterProjectionCheckpoint, ContactCenterProjectionCheckpointIndex>(
            index => index.HandlerId == handlerId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
