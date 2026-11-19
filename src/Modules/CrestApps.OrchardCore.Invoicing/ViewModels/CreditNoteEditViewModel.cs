using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The draft credit note editor.
/// </summary>
public class CreditNoteEditViewModel
{
    public string ItemId { get; set; }

    public string Reason { get; set; }

    public CreditNoteDisposition Disposition { get; set; }

    public string Notes { get; set; }

    public List<InvoiceLineEditViewModel> Lines { get; set; } = [];

    [BindNever]
    public string InvoiceId { get; set; }

    [BindNever]
    public string InvoiceNumber { get; set; }

    [BindNever]
    public string Currency { get; set; }

    [BindNever]
    public DocumentTotals Totals { get; set; }

    [BindNever]
    public IList<DocumentTax> Taxes { get; set; } = [];

    [BindNever]
    public decimal Creditable { get; set; }

    [BindNever]
    public List<SelectListItem> Dispositions { get; set; } = [];

    [BindNever]
    public bool CanIssue { get; set; }
}
