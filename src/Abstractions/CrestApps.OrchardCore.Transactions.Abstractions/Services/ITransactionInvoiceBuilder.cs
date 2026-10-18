using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Builds the invoice for a transaction its owner pays themselves.
/// </summary>
/// <remarks>
/// The Transactions module registers the default implementation whenever the Receipts module is enabled too, so
/// resolve it as optional: without the Receipts module there is no invoice to build.
/// </remarks>
public interface ITransactionInvoiceBuilder
{
    /// <summary>
    /// Builds the invoice for <paramref name="transaction"/>: what it is for, its tax, its total, and what is still due.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ReceiptDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
