using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores open shifts.
/// </summary>
internal sealed class OpenShiftStore : DocumentCatalog<OpenShift, OpenShiftIndex>
{
    public OpenShiftStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<OpenShift>> ListForPeriodAsync(string periodId, CancellationToken cancellationToken = default)
    {
        var shifts = await Session.Query<OpenShift, OpenShiftIndex>(index => index.PeriodId == periodId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return shifts.ToArray();
    }

    public async Task<IReadOnlyList<OpenShift>> ListDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var shifts = await Session.Query<OpenShift, OpenShiftIndex>(index => index.ClaimPolicy == OpenShiftClaimPolicy.BySeniority
                && (index.Status == OpenShiftStatus.Open || index.Status == OpenShiftStatus.Claimed)
                && index.ClaimDeadlineUtc != null
                && index.ClaimDeadlineUtc <= nowUtc, collection: CollectionName)
            .ListAsync(cancellationToken);

        return shifts.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<OpenShift> Items)> PageAsync(OpenShiftQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<OpenShift, OpenShiftIndex>(collection: CollectionName);

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
        else
        {
            search = search.Where(index => index.Status == OpenShiftStatus.Open || index.Status == OpenShiftStatus.Claimed);
        }

        if (query.From.HasValue)
        {
            var from = WorkforceDates.ToKey(query.From.Value);

            search = search.Where(index => index.Date >= from);
        }

        if (!string.IsNullOrEmpty(query.PeriodId))
        {
            var periodId = query.PeriodId;

            search = search.Where(index => index.PeriodId == periodId);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderBy(index => index.StartUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
