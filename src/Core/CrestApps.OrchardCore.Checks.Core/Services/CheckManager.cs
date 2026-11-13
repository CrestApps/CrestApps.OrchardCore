using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checks.Core.Services;

/// <summary>
/// Manages the check register, so its handlers run.
/// </summary>
public sealed class CheckManager : CatalogManager<CheckRecord>, ICheckManager
{
    private readonly ICheckRecordStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public CheckManager(
        ICheckRecordStore store,
        IEnumerable<ICatalogEntryHandler<CheckRecord>> handlers,
        ILogger<CatalogManager<CheckRecord>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<CheckRecord> FindByNumberAsync(string bankAccountId, long number, CancellationToken cancellationToken = default)
        => _store.FindByNumberAsync(bankAccountId, number, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CheckRecord>> ListBySourceAsync(string sourceKind, string sourceId, CancellationToken cancellationToken = default)
        => _store.ListBySourceAsync(sourceKind, sourceId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckRecord>> ListAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var checks = await _store.GetAsync(ids.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal), cancellationToken);

        return checks.OrderBy(check => check.BankAccountId, StringComparer.Ordinal).ThenBy(check => check.Number).ToList();
    }

    /// <inheritdoc />
    public Task<long?> GetHighestNumberAsync(string bankAccountId, CancellationToken cancellationToken = default)
        => _store.GetHighestNumberAsync(bankAccountId, cancellationToken);
}
