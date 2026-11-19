using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Builds customers' statements from their issued invoices, credit notes and the ledger's payments, prints them as a
/// business document (kind <c>InvoiceStatement</c>), and e-mails them with the PDF.
/// </summary>
public interface IInvoiceStatementService
{
    /// <summary>
    /// Builds a statement. Void invoices are left out; a payment, an adjustment, a payment taken back or a refund is an
    /// entry of the day it happened in the site's time zone.
    /// </summary>
    /// <param name="request">The customer, kind, period and currency.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceStatement> BuildAsync(InvoiceStatementRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prints a statement as a business document.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<BusinessDocument> BuildDocumentAsync(InvoiceStatement statement, CancellationToken cancellationToken = default);

    /// <summary>
    /// E-mails a statement to an address (the customer's, from their latest invoice, when none is given) with its PDF.
    /// Returns why it could not be sent, or <see langword="null"/> when it was.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="to">The address, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> SendAsync(InvoiceStatement statement, string to = null, CancellationToken cancellationToken = default);
}
