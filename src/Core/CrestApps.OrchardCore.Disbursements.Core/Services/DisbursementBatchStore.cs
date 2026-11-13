using CrestApps.OrchardCore.Disbursements.Core.Indexes;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Stores disbursement batches in YesSql.
/// </summary>
public sealed class DisbursementBatchStore : DocumentCatalog<DisbursementBatch, DisbursementBatchIndex>, IDisbursementBatchStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public DisbursementBatchStore(ISession session)
        : base(session)
    {
        CollectionName = DisbursementsConstants.CollectionName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisbursementBatch>> ListReleasedAsync(string provider, DateTime releasedAfterUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);

        var batches = await Session.Query<DisbursementBatch, DisbursementBatchIndex>(index => index.Provider == provider && index.ReleasedUtc != null && index.ReleasedUtc > releasedAfterUtc, collection: CollectionName)
            .OrderBy(index => index.ReleasedUtc)
            .ListAsync(cancellationToken);

        return batches.ToList();
    }

    /// <inheritdoc />
    public Task<int> CountReleasedAsync(string sourceBankAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceBankAccountId);

        return Session.QueryIndex<DisbursementBatchIndex>(index => index.SourceBankAccountId == sourceBankAccountId && index.ReleasedUtc >= fromUtc && index.ReleasedUtc < toUtc, collection: CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisbursementBatch>> ListToSettleAsync(DateTime releasedBeforeUtc, int take, CancellationToken cancellationToken = default)
    {
        var released = nameof(DisbursementBatchStatus.Released);
        var batches = await Session.Query<DisbursementBatch, DisbursementBatchIndex>(index => index.Status == released && index.ReleasedUtc < releasedBeforeUtc, collection: CollectionName)
            .OrderBy(index => index.ReleasedUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return batches.ToList();
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<DisbursementBatch> Batches)> ListPageAsync(DisbursementBatchStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var batches = Session.Query<DisbursementBatch, DisbursementBatchIndex>(collection: CollectionName);

        if (status is { } value)
        {
            var name = value.ToString();
            batches = batches.Where(index => index.Status == name);
        }

        var count = await batches.CountAsync(cancellationToken);
        var page = await batches.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }
}
