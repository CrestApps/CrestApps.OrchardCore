using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores schedule periods.
/// </summary>
internal sealed class SchedulePeriodStore : DocumentCatalog<SchedulePeriod, SchedulePeriodIndex>
{
    public SchedulePeriodStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<SchedulePeriod> Items)> PageAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<SchedulePeriod, SchedulePeriodIndex>(collection: CollectionName);

        if (teamIds is not null)
        {
            if (teamIds.Count == 0)
            {
                return (0, []);
            }

            query = query.Where(index => index.TeamId.IsIn(teamIds));
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.StartDate).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
