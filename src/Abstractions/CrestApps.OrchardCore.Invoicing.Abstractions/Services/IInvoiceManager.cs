using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Stores and finds invoices. Creating, changing and deleting go through the catalog handlers, which refuse a change to
/// an issued invoice: after issue only <see cref="IInvoiceCommandService"/> changes it.
/// </summary>
public interface IInvoiceManager : ICatalogManager<Invoice>
{
    /// <summary>
    /// Finds the invoice that posted a transaction.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Invoice> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an issued invoice by its number.
    /// </summary>
    /// <param name="number">The invoice number, such as <c>INV-1001</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Invoice> FindByNumberAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of invoices, newest first.
    /// </summary>
    /// <param name="page">The page number, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="query">The filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PageResult<Invoice>> PageAsync(int page, int pageSize, InvoiceQuery query, CancellationToken cancellationToken = default);
}
