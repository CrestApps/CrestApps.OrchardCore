using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Turns an invoice or a credit note into the printable business document its pages, PDF and e-mail show, with the
/// document template of its kind applied.
/// </summary>
public interface IInvoiceDocumentBuilder
{
    /// <summary>
    /// Builds the business document of an invoice or a credit note. A draft carries a watermark.
    /// </summary>
    /// <param name="document">The invoice or credit note.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<BusinessDocument> BuildAsync(InvoicingDocument document, CancellationToken cancellationToken = default);
}
