using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The printable page of an invoice or credit note.
/// </summary>
public class InvoiceDocumentPageViewModel
{
    public InvoicingDocument Source { get; set; }

    public BusinessDocument Document { get; set; }

    public string PdfUrl { get; set; }

    public string PayUrl { get; set; }

    public string BackUrl { get; set; }

    public bool CanPay { get; set; }
}
