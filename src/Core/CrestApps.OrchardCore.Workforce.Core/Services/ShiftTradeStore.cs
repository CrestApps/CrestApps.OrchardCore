using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores shift trades.
/// </summary>
internal sealed class ShiftTradeStore : DocumentCatalog<ShiftTrade, ShiftTradeIndex>
{
    public ShiftTradeStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <summary>
    /// Lists the trades still waiting on someone that involve a shift, as the offered shift or the one asked for.
    /// </summary>
    public async Task<IReadOnlyList<ShiftTrade>> ListPendingForShiftAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var trades = await Session.Query<ShiftTrade, ShiftTradeIndex>(index => (index.ShiftEventId == eventId || index.TargetShiftEventId == eventId)
                && (index.Status == ShiftTradeStatus.Offered || index.Status == ShiftTradeStatus.Accepted), collection: CollectionName)
            .ListAsync(cancellationToken);

        return trades.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<ShiftTrade> Items)> PageAsync(ShiftTradeQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<ShiftTrade, ShiftTradeIndex>(collection: CollectionName);

        if (query.TeamIds is not null)
        {
            if (query.TeamIds.Count == 0)
            {
                return (0, []);
            }

            search = search.Where(index => index.TeamId.IsIn(query.TeamIds));
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            search = search.Where(index => index.Status == status);
        }

        if (!string.IsNullOrEmpty(query.WorkerId))
        {
            var workerId = query.WorkerId;

            search = search.Where(index => index.OfferedByWorkerId == workerId || index.TargetWorkerId == workerId);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
