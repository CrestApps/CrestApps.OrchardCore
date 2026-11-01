using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores predictability pay entries.
/// </summary>
internal sealed class PredictabilityPayStore : DocumentCatalog<PredictabilityPayEntry, PredictabilityPayEntryIndex>
{
    public PredictabilityPayStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<PredictabilityPayEntry>> ListForChangeAsync(string scheduleChangeId, CancellationToken cancellationToken = default)
    {
        var entries = await Session.Query<PredictabilityPayEntry, PredictabilityPayEntryIndex>(index => index.ScheduleChangeId == scheduleChangeId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return entries.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<PredictabilityPayEntry> Items)> PageAsync(PredictabilityPayQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<PredictabilityPayEntry, PredictabilityPayEntryIndex>(collection: CollectionName);

        if (query.TeamIds is not null)
        {
            if (query.TeamIds.Count == 0)
            {
                return (0, []);
            }

            search = search.Where(index => index.TeamId.IsIn(query.TeamIds));
        }

        if (!string.IsNullOrEmpty(query.WorkerId))
        {
            var workerId = query.WorkerId;

            search = search.Where(index => index.WorkerId == workerId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            search = search.Where(index => index.Status == status);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
