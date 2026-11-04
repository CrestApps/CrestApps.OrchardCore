using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores published schedule changes.
/// </summary>
internal sealed class ScheduleChangeStore : DocumentCatalog<ScheduleChange, ScheduleChangeIndex>
{
    public ScheduleChangeStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<ScheduleChange>> ListForPeriodAsync(string periodId, CancellationToken cancellationToken = default)
    {
        var changes = await Session.Query<ScheduleChange, ScheduleChangeIndex>(index => index.PeriodId == periodId, collection: CollectionName)
            .OrderBy(index => index.ChangedUtc)
            .ListAsync(cancellationToken);

        return changes.ToArray();
    }
}
