using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterWorkStateStore"/>.
/// </summary>
public sealed class ContactCenterWorkStateStore : ConcurrentDocumentCatalog<ContactCenterWorkState, ContactCenterWorkStateIndex>, IContactCenterWorkStateStore
{
    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterWorkStateStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public Task<ContactCenterWorkState> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        return Session.Query<ContactCenterWorkState, ContactCenterWorkStateIndex>(
            index => index.ActivityItemId == activityItemId,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterWorkState>> GetByActivityIdsAsync(
        IEnumerable<string> activityItemIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityItemIds);

        var ids = activityItemIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var states = await Session.Query<ContactCenterWorkState, ContactCenterWorkStateIndex>(
            index => index.ActivityItemId.IsIn(ids),
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return states.ToArray();
    }
}
