using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IInvoiceManager"/>.
/// </summary>
public sealed class InvoiceManager : CatalogManager<Invoice>, IInvoiceManager
{
    private readonly IInvoiceStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceManager"/> class.
    /// </summary>
    /// <param name="store">The invoice store.</param>
    /// <param name="handlers">The catalog handlers, which validate drafts and refuse changes to issued invoices.</param>
    /// <param name="logger">The logger.</param>
    public InvoiceManager(
        IInvoiceStore store,
        IEnumerable<ICatalogEntryHandler<Invoice>> handlers,
        ILogger<CatalogManager<Invoice>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task<Invoice> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
        => _store.FindByTransactionIdAsync(transactionId, cancellationToken);

    /// <inheritdoc/>
    public Task<Invoice> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
        => _store.FindByNumberAsync(number, cancellationToken);

    /// <inheritdoc/>
    public Task<PageResult<Invoice>> PageAsync(int page, int pageSize, InvoiceQuery query, CancellationToken cancellationToken = default)
        => _store.PageAsync(page, pageSize, query, cancellationToken);
}
