using CrestApps.OrchardCore.Disbursements.Core.Indexes;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.YesSql.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.SourceAccess;
using YesSql;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Stores disbursements in YesSql.
/// </summary>
public sealed class DisbursementStore : DocumentCatalog<Disbursement, DisbursementIndex>, IDisbursementStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public DisbursementStore(ISession session)
        : base(session)
    {
        CollectionName = DisbursementsConstants.CollectionName;
    }

    /// <inheritdoc />
    public Task<Disbursement> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return Session.Query<Disbursement, DisbursementIndex>(index => index.IdempotencyKey == key, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Disbursement> FindByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(traceNumber);

        return Session.Query<Disbursement, DisbursementIndex>(index => index.TraceNumber == traceNumber, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Disbursement> FindByCheckIdAsync(string checkId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(checkId);

        return Session.Query<Disbursement, DisbursementIndex>(index => index.CheckId == checkId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Disbursement>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceKind);
        ArgumentException.ThrowIfNullOrEmpty(sourceId);

        var items = await Session.Query<Disbursement, DisbursementIndex>(index => index.SourceKind == sourceKind && index.SourceId == sourceId, collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<bool> UsesBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bankAccountId);

        return await Session.QueryIndex<DisbursementIndex>(index => index.SourceBankAccountId == bankAccountId || index.DestinationBankAccountId == bankAccountId, collection: CollectionName)
            .CountAsync(cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<Disbursement> Disbursements)> ListPageAsync(DisbursementQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = Session.Query<Disbursement, DisbursementIndex>(collection: CollectionName);

        if (SourceAccessExpressions.Restrict<DisbursementIndex>(query.Access ?? SourceAccessFilter.Unrestricted) is { } restrict)
        {
            items = items.Where(restrict);
        }

        if (query.Status is { } status)
        {
            var statusName = status.ToString();
            items = items.Where(index => index.Status == statusName);
        }

        if (!string.IsNullOrEmpty(query.Method))
        {
            var method = query.Method;
            items = items.Where(index => index.Method == method);
        }

        if (!string.IsNullOrEmpty(query.BatchId))
        {
            var batchId = query.BatchId;
            items = items.Where(index => index.BatchId == batchId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            items = items.Where(index => index.PayeeName.Contains(search) || index.TraceNumber == search);
        }

        var count = await items.CountAsync(cancellationToken);
        var ordered = query.OldestFirst
            ? items.OrderBy(index => index.CreatedUtc).ThenBy(index => index.Id)
            : items.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id);
        var page = await ordered.Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }
}
