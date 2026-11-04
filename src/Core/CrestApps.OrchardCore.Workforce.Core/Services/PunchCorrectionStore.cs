using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores workers' punch corrections.
/// </summary>
internal sealed class PunchCorrectionStore : DocumentCatalog<PunchCorrection, PunchCorrectionIndex>
{
    public PunchCorrectionStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<PunchCorrection>> ListAsync(IReadOnlyCollection<string> teamIds, string workerId, PunchCorrectionStatus? status, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<PunchCorrection, PunchCorrectionIndex>(collection: CollectionName);

        if (teamIds is not null)
        {
            if (teamIds.Count == 0)
            {
                return [];
            }

            search = search.Where(index => index.TeamId.IsIn(teamIds));
        }

        if (!string.IsNullOrEmpty(workerId))
        {
            search = search.Where(index => index.WorkerId == workerId);
        }

        if (status.HasValue)
        {
            var value = status.Value;

            search = search.Where(index => index.Status == value);
        }

        var items = await search.OrderBy(index => index.CreatedUtc).Take(500).ListAsync(cancellationToken);

        return items.ToArray();
    }
}
