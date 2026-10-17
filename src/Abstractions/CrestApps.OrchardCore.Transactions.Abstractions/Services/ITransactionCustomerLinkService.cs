using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Creates the links a customer is sent to their own statement, invoices and receipts, for example in a payment
/// reminder or a receipt email. The site decides whether they open the customer pages in the site theme or the
/// customer pages in the admin, through the <c>CrestApps:Transactions:CustomerPages:LinkTarget</c> option.
/// </summary>
/// <remarks>
/// Every link opens a page that asks the customer to sign in and shows only their own transactions, so these links are
/// for transactions owned by a signed-up customer. A guest has no account: use <see cref="ITransactionPayLinkService"/>.
/// </remarks>
public interface ITransactionCustomerLinkService
{
    /// <summary>
    /// Creates the absolute address of the customer's statement, or returns <see langword="null"/> when the site's
    /// address is not known (no request, and no base URL in the site settings).
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetStatementUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the absolute address of the invoice page of <paramref name="transaction"/>, or returns
    /// <see langword="null"/> when the site's address is not known.
    /// </summary>
    /// <param name="transaction">The invoiced transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetInvoiceUrlAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the absolute address of the receipt page of the payment <paramref name="payment"/>, or returns
    /// <see langword="null"/> when the payment has no identifier or the site's address is not known.
    /// </summary>
    /// <param name="transaction">The transaction the payment was applied to.</param>
    /// <param name="payment">The timeline event that recorded the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetReceiptUrlAsync(Transaction transaction, TransactionEvent payment, CancellationToken cancellationToken = default);
}
