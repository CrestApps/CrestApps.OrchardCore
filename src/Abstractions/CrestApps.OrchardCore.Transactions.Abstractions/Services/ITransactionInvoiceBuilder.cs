using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Builds the printable payment request of a transaction its owner pays themselves (shown to customers as its invoice):
/// a business document of the kind <see cref="TransactionsConstants.BusinessDocumentKinds.PaymentRequest"/>, rendered
/// with the <c>BusinessDocument</c> shape, the HTML renderer and the PDF renderer like every other business document.
/// It is the seam an invoicing module replaces, so a transaction posted for a real invoice shows that invoice.
/// </summary>
/// <remarks>
/// The Transactions module registers the default implementation whenever the Receipts module is enabled too, so
/// resolve it as optional: without the Receipts module there is no payment request to build.
/// </remarks>
public interface ITransactionInvoiceBuilder
{
    /// <summary>
    /// Builds the payment request for <paramref name="transaction"/>: what it is for, its tax, its total and what is
    /// still due, in the look of the document template for payment requests.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<BusinessDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
