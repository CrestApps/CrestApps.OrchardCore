using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterWorkStateStore"/>.
/// </summary>
public sealed class ContactCenterWorkStateStore : DocumentCatalog<ContactCenterWorkState, ContactCenterWorkStateIndex>, IContactCenterWorkStateStore
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
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public Task<ContactCenterWorkState> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityItemId);

        return Session.Query<ContactCenterWorkState, ContactCenterWorkStateIndex>(
            index => index.ActivityItemId == activityItemId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterWorkState>> ListByActivityIdsAsync(
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
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return states.ToArray();
    }
}
