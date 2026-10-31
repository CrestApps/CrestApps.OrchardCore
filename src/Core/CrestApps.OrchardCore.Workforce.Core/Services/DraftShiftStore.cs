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

    /// <summary>
    /// Finds the draft shift of a period with a shift key, on the day it belongs to.
    /// </summary>
    public async Task<DraftShift> FindByShiftKeyAsync(string periodId, DateOnly date, string shiftKey, CancellationToken cancellationToken = default)
    {
        var dateKey = WorkforceDates.ToKey(date);
        var shifts = await Session.Query<DraftShift, DraftShiftIndex>(index => index.PeriodId == periodId && index.Date == dateKey, collection: CollectionName)
            .ListAsync(cancellationToken);

        return shifts.FirstOrDefault(shift => shift.ShiftKey == shiftKey);
    }

    public void Delete(DraftShift shift)
    {
        Session.Delete(shift, CollectionName);
    }
}
