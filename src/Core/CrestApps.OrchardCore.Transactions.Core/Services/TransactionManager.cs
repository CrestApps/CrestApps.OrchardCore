using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="ITransactionManager"/>. It delegates storage to <see cref="ITransactionStore"/>
/// and runs loaded entries through the registered catalog handlers.
/// </summary>
public sealed class TransactionManager : CatalogManager<Transaction>, ITransactionManager
{
    private readonly ITransactionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying transaction store.</param>
    /// <param name="handlers">The catalog entry handlers for transaction entries.</param>
    /// <param name="logger">The logger instance.</param>
    public TransactionManager(
        ITransactionStore store,
        IEnumerable<ICatalogEntryHandler<Transaction>> handlers,
        ILogger<CatalogManager<Transaction>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<PageResult<Transaction>> PageAsync(int page, int pageSize, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _store.PageAsync(page, pageSize, query, cancellationToken);

        foreach (var entry in result.Entries)
        {
            await LoadAsync(entry, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<Transaction> GetByObligationAsync(string checkoutSessionId, string obligationId, CancellationToken cancellationToken = default)
    {
        var transaction = await _store.GetByObligationAsync(checkoutSessionId, obligationId, cancellationToken);

        if (transaction is not null)
        {
            await LoadAsync(transaction, cancellationToken);
        }

        return transaction;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetOutstandingDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var transactions = await _store.GetOutstandingDueAsync(asOfUtc, cancellationToken);

        foreach (var transaction in transactions)
        {
            await LoadAsync(transaction, cancellationToken);
        }

        return transactions;
    }
}
