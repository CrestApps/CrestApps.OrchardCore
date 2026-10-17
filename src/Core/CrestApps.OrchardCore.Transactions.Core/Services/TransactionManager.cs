using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="ITransactionManager"/>. It delegates storage to <see cref="ITransactionStore"/>, runs loaded
/// entries through the registered catalog handlers, and raises the <see cref="ITransactionEventHandler"/> lifecycle
/// events after a transaction is created or a save changes its status.
/// </summary>
public sealed class TransactionManager : CatalogManager<Transaction>, ITransactionManager
{
    private readonly ITransactionStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    // The status each transaction had when it was loaded or last saved through this manager, keyed by the instance so a
    // later save can tell whether it changed the status. The table holds the entries weakly, so a transaction that is
    // no longer used is not kept alive by it.
    private readonly ConditionalWeakTable<Transaction, StrongBox<TransactionStatus>> _savedStatuses = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionManager"/> class.
    /// </summary>
    /// <param name="store">The underlying transaction store.</param>
    /// <param name="handlers">The catalog entry handlers for transaction entries.</param>
    /// <param name="serviceProvider">The service provider the lifecycle event handlers are resolved from when an event is
    /// raised. They are resolved then rather than injected, because a handler may itself depend on the transaction
    /// manager.</param>
    /// <param name="logger">The logger instance.</param>
    public TransactionManager(
        ITransactionStore store,
        IEnumerable<ICatalogEntryHandler<Transaction>> handlers,
        IServiceProvider serviceProvider,
        ILogger<CatalogManager<Transaction>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates the transaction, then raises <see cref="ITransactionEventHandler.CreatedAsync"/>.
    /// </summary>
    /// <param name="entry">The transaction to create.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public new async ValueTask CreateAsync(Transaction entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await base.CreateAsync(entry, cancellationToken);

        RememberSavedStatus(entry);

        await GetEventHandlers().CreatedAsync(entry, _logger, cancellationToken);
    }

    /// <summary>
    /// Saves the transaction, then raises <see cref="ITransactionEventHandler.StatusChangedAsync"/> when the save changed
    /// its status (and <see cref="ITransactionEventHandler.CanceledAsync"/> when the new status is
    /// <see cref="TransactionStatus.Canceled"/>). A save that leaves the status as it was raises nothing.
    /// </summary>
    /// <param name="entry">The transaction to save.</param>
    /// <param name="data">Optional field changes passed to the catalog handlers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public new async ValueTask UpdateAsync(Transaction entry, JsonNode data = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var known = _savedStatuses.TryGetValue(entry, out var saved);
        var previous = known ? saved.Value : entry.Status;

        await base.UpdateAsync(entry, data, cancellationToken);

        // Remembered before the handlers run, so a handler that saves the same transaction again without changing its
        // status raises nothing more.
        RememberSavedStatus(entry);

        if (!known)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Transaction '{TransactionId}' was saved without being loaded through the transaction manager, so no status change is raised for it.", entry.ItemId);
            }

            return;
        }

        var current = entry.Status;

        if (previous == current)
        {
            return;
        }

        var handlers = GetEventHandlers();

        await handlers.StatusChangedAsync(entry, previous, current, _logger, cancellationToken);

        if (current == TransactionStatus.Canceled)
        {
            await handlers.CanceledAsync(entry, TransactionEventHandlerExtensions.GetCancellationReason(entry), _logger, cancellationToken);
        }
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetComingDueAsync(DateTime afterUtc, DateTime untilUtc, CancellationToken cancellationToken = default)
    {
        var transactions = await _store.GetComingDueAsync(afterUtc, untilUtc, cancellationToken);

        foreach (var transaction in transactions)
        {
            await LoadAsync(transaction, cancellationToken);
        }

        return transactions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Transaction>> GetDueForRenewalAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var transactions = await _store.GetDueForRenewalAsync(asOfUtc, cancellationToken);

        foreach (var transaction in transactions)
        {
            await LoadAsync(transaction, cancellationToken);
        }

        return transactions;
    }

    /// <inheritdoc/>
    protected override async Task LoadAsync(Transaction entry, CancellationToken cancellationToken = default)
    {
        // The status as stored. A transaction the session already holds (the same instance loaded again) keeps the status
        // it was saved with, so a change made to it since then is still reported when it is saved.
        _savedStatuses.TryAdd(entry, new StrongBox<TransactionStatus>(entry.Status));

        await base.LoadAsync(entry, cancellationToken);
    }

    private void RememberSavedStatus(Transaction entry)
        => _savedStatuses.AddOrUpdate(entry, new StrongBox<TransactionStatus>(entry.Status));

    private IEnumerable<ITransactionEventHandler> GetEventHandlers()
        => _serviceProvider?.GetServices<ITransactionEventHandler>() ?? [];
}
