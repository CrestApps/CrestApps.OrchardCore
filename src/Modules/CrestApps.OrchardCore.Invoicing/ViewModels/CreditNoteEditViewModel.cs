using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The draft credit note editor.
/// </summary>
public class CreditNoteEditViewModel
{
    /// <summary>
    /// Gets or sets the editor of the page, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

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
