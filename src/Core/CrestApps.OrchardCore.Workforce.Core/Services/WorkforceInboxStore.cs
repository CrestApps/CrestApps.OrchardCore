using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores the portal inbox.
/// </summary>
internal sealed class WorkforceInboxStore : DocumentCatalog<WorkforceInboxItem, WorkforceInboxIndex>
{
    private const int MaxUnreadMarked = 500;

    public WorkforceInboxStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<(int Count, int Unread, IReadOnlyList<WorkforceInboxItem> Items)> PageAsync(string workerId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<WorkforceInboxItem, WorkforceInboxIndex>(index => index.WorkerId == workerId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var unread = await Session.Query<WorkforceInboxItem, WorkforceInboxIndex>(index => index.WorkerId == workerId && !index.IsRead, collection: CollectionName).CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, unread, page.ToArray());
    }

    public async Task<IReadOnlyList<WorkforceInboxItem>> ListUnreadAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<WorkforceInboxItem, WorkforceInboxIndex>(index => index.WorkerId == workerId && !index.IsRead, collection: CollectionName)
            .Take(MaxUnreadMarked)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }
}
