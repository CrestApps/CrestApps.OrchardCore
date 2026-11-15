using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Finds the invoice a ledger transaction belongs to: the one that posted it (its reference names the invoice), or the
/// one that took it over when it was issued for a debt already on the ledger, such as an order paid on account.
/// </summary>
public static class InvoiceTransactionLink
{
    /// <summary>
    /// Gets a value indicating whether an invoice posted the transaction.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    public static bool IsPostedByInvoice(Transaction transaction)
        => transaction is not null &&
            string.Equals(transaction.ReferenceType, InvoicingConstants.InvoiceReferenceType, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(transaction.ReferenceId);

    /// <summary>
    /// Gets the invoice the transaction belongs to, or <see langword="null"/> when none does.
    /// </summary>
    /// <param name="invoices">The invoice store.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<Invoice> FindInvoiceAsync(IInvoiceStore invoices, Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        if (transaction is null || string.IsNullOrEmpty(transaction.ItemId))
        {
            return null;
        }

        return IsPostedByInvoice(transaction)
            ? await invoices.FindByIdAsync(transaction.ReferenceId, cancellationToken)
            : await invoices.FindByTransactionIdAsync(transaction.ItemId, cancellationToken);
    }
}
