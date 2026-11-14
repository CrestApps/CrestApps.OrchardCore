using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Adds to the business document of an invoice or credit note before its template is applied: a work order adds its job
/// address, a project its name.
/// </summary>
public interface IInvoiceDocumentContributor
{
    /// <summary>
    /// Adds fields, sections or rows to the business document built for <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The invoice or credit note.</param>
    /// <param name="document">The business document being built.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task ContributeAsync(InvoicingDocument source, BusinessDocument document, CancellationToken cancellationToken = default);
}
