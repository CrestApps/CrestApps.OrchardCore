using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterOutboxStore"/>.
/// </summary>
public sealed class ContactCenterOutboxStore : DocumentCatalog<ContactCenterOutboxMessage, ContactCenterOutboxMessageIndex>, IContactCenterOutboxStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterOutboxStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterOutboxMessage>> ListDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? 100 : maxCount;

        var due = await Session.Query<ContactCenterOutboxMessage, ContactCenterOutboxMessageIndex>(
            index => index.Status == OutboxMessageStatus.Pending && index.NextAttemptUtc <= nowUtc,
            collection: ContactCenterConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return due.ToArray();
    }
}
