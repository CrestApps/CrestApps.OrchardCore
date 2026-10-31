using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores schedule scenarios.
/// </summary>
internal sealed class ScheduleScenarioStore : DocumentCatalog<ScheduleScenario, ScheduleScenarioIndex>
{
    public ScheduleScenarioStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<ScheduleScenario>> ListForPeriodAsync(string periodId, CancellationToken cancellationToken = default)
    {
        var scenarios = await Session.Query<ScheduleScenario, ScheduleScenarioIndex>(index => index.PeriodId == periodId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return scenarios.ToArray();
    }
}
