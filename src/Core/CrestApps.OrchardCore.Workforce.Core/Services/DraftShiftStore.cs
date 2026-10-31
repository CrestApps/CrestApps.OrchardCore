using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores draft shifts.
/// </summary>
internal sealed class DraftShiftStore : DocumentCatalog<DraftShift, DraftShiftIndex>
{
    public DraftShiftStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<DraftShift>> ListForScenarioAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        var shifts = await Session.Query<DraftShift, DraftShiftIndex>(index => index.ScenarioId == scenarioId, collection: CollectionName)
            .OrderBy(index => index.Date)
            .ListAsync(cancellationToken);

        return shifts.OrderBy(shift => shift.Date).ThenBy(shift => shift.Start).ToArray();
    }

    public void Delete(DraftShift shift)
    {
        Session.Delete(shift, CollectionName);
    }
}
