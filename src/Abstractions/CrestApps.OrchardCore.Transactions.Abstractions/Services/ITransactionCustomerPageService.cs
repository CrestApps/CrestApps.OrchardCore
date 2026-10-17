using System.Security.Claims;
using CrestApps.OrchardCore.Transactions.ViewModels;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Builds what a customer sees of their own transactions: their statement, an invoice, and the receipt of a payment.
/// It applies the same ownership rules as the customer pages in the admin, so a module or a theme can show these
/// documents anywhere, in the site theme, without giving the customer admin access.
/// </summary>
/// <remarks>
/// <para>
/// A customer only ever sees their own transactions: one owned by their signed-in account (a guest transaction is
/// reached through its signed pay link instead). A user with the "Manage transactions" permission can open any
/// invoice or receipt, and the model then says so (<c>IsAdministrator</c>). The statement always lists the viewer's
/// own transactions.
/// </para>
/// <para>
/// The result is <see cref="TransactionCustomerPageStatus.Forbidden"/> when the viewer has neither permission, and
/// <see cref="TransactionCustomerPageStatus.NotFound"/> when the document does not exist, belongs to someone else, or
/// cannot be built (invoices and receipts need the Receipts module). Someone else's transaction is reported as not
/// found rather than forbidden, so identifiers cannot be probed.
/// </para>
/// <para>
/// Render a model through its shape (<see cref="TransactionsConstants.Shapes"/>), for example
/// <c>shapeFactory.CreateAsync&lt;TransactionInvoiceViewModel&gt;(TransactionsConstants.Shapes.Invoice, result.Model.CopyTo)</c>,
/// so a theme can override how it looks.
/// </para>
/// </remarks>
public interface ITransactionCustomerPageService
{
    /// <summary>
    /// Builds one page of the statement of the transactions <paramref name="user"/> owns.
    /// </summary>
    /// <param name="user">The viewer.</param>
    /// <param name="request">The page and filter to build.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCustomerPageResult<TransactionStatementViewModel>> BuildStatementAsync(
        ClaimsPrincipal user,
        TransactionStatementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the invoice of the transaction <paramref name="transactionId"/>, when <paramref name="user"/> may see it.
    /// </summary>
    /// <param name="user">The viewer.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCustomerPageResult<TransactionInvoiceViewModel>> BuildInvoiceAsync(
        ClaimsPrincipal user,
        string transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the receipt of the payment <paramref name="paymentId"/> on the transaction <paramref name="transactionId"/>,
    /// when <paramref name="user"/> may see it.
    /// </summary>
    /// <param name="user">The viewer.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="paymentId">The identifier of the timeline event that recorded the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCustomerPageResult<TransactionReceiptViewModel>> BuildReceiptAsync(
        ClaimsPrincipal user,
        string transactionId,
        string paymentId,
        CancellationToken cancellationToken = default);
}
