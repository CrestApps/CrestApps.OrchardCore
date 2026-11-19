using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Services;

/// <summary>
/// Manages disbursements, so their handlers run.
/// </summary>
public sealed class DisbursementManager : CatalogManager<Disbursement>, IDisbursementManager
{
    private readonly IDisbursementStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public DisbursementManager(IDisbursementStore store, IEnumerable<ICatalogEntryHandler<Disbursement>> handlers, ILogger<CatalogManager<Disbursement>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<Disbursement> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default)
        => _store.FindByIdempotencyKeyAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Disbursement> FindByTraceNumberAsync(string traceNumber, CancellationToken cancellationToken = default)
        => _store.FindByTraceNumberAsync(traceNumber, cancellationToken);

    /// <inheritdoc />
    public Task<Disbursement> FindByCheckIdAsync(string checkId, CancellationToken cancellationToken = default)
        => _store.FindByCheckIdAsync(checkId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Disbursement>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => _store.ListBySourceAsync(sourceKind, sourceId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Disbursement>> ListAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = await _store.GetAsync(ids.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal), cancellationToken);

        return items.OrderBy(item => item.CreatedUtc).ThenBy(item => item.ItemId, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Manages disbursement batches, so their handlers run.
/// </summary>
public sealed class DisbursementBatchManager : CatalogManager<DisbursementBatch>, IDisbursementBatchManager
{
    private readonly IDisbursementBatchStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBatchManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public DisbursementBatchManager(IDisbursementBatchStore store, IEnumerable<ICatalogEntryHandler<DisbursementBatch>> handlers, ILogger<CatalogManager<DisbursementBatch>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DisbursementBatch>> ListReleasedAsync(string provider, DateTime releasedAfterUtc, CancellationToken cancellationToken = default)
        => _store.ListReleasedAsync(provider, releasedAfterUtc, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountReleasedAsync(string sourceBankAccountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => _store.CountReleasedAsync(sourceBankAccountId, fromUtc, toUtc, cancellationToken);
}
