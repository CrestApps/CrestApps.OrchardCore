using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Keeps the files the time clock's payroll export wrote before the export became a data pipeline, to download until
/// they age out.
/// </summary>
public sealed class PayrollExportBatchStore : DocumentCatalog<PayrollExportBatch, PayrollExportBatchIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollExportBatchStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayrollExportBatchStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <summary>
    /// Reads a page of the files, the newest first.
    /// </summary>
    /// <param name="skip">The files to skip.</param>
    /// <param name="take">The files to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count of every file and the page.</returns>
    public async Task<(int Count, IReadOnlyList<PayrollExportBatch> Items)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<PayrollExportBatch, PayrollExportBatchIndex>(collection: CollectionName);
        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
