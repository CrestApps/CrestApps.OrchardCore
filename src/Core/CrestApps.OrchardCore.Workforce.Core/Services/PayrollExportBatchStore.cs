using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores payroll exports.
/// </summary>
internal sealed class PayrollExportBatchStore : DocumentCatalog<PayrollExportBatch, PayrollExportBatchIndex>
{
    public PayrollExportBatchStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlySet<string>> ListIdsAsync(string providerName, string calendarId, DateOnly periodStart, CancellationToken cancellationToken = default)
    {
        var start = WorkforceDates.ToKey(periodStart);
        var rows = await Session.QueryIndex<PayrollExportBatchIndex>(index => index.ProviderName == providerName && index.PayPeriodCalendarId == calendarId && index.PeriodStart == start, collection: CollectionName)
            .ListAsync(cancellationToken);

        return rows.Select(row => row.ItemId).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<(int Count, IReadOnlyList<PayrollExportBatch> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<PayrollExportBatch, PayrollExportBatchIndex>(collection: CollectionName);
        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
