using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Numbers the invoices of transactions their owners pay themselves.
/// </summary>
public interface ITransactionInvoiceService
{
    /// <summary>
    /// Gives <paramref name="transaction"/> an invoice number, for example <c>INV-1001</c>, unless it already has one.
    /// The caller saves the transaction. A transaction that is charged automatically is not invoiced.
    /// </summary>
    /// <param name="transaction">The transaction to number.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a number was issued now.</returns>
    Task<bool> EnsureInvoiceNumberAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
