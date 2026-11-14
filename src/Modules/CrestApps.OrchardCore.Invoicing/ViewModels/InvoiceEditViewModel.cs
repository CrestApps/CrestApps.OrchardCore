using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The draft invoice editor.
/// </summary>
public class InvoiceEditViewModel
{
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets who the invoice is billed to, as a party's <c>Kind:Id</c>, or empty for someone known only by the
    /// contact typed.
    /// </summary>
    public string BillTo { get; set; }

    public string ContactName { get; set; }

    public string ContactEmail { get; set; }

    public string Currency { get; set; }

    public string PaymentTermId { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public string Reference { get; set; }

    public bool PricesIncludeTax { get; set; }

    public bool AllowPartialPayment { get; set; }

    public string Notes { get; set; }

    public string Terms { get; set; }

    public string Footer { get; set; }

    public string InternalNote { get; set; }

    public List<InvoiceLineEditViewModel> Lines { get; set; } = [];

    [BindNever]
    public string BillToName { get; set; }

    [BindNever]
    public DocumentTotals Totals { get; set; }

    [BindNever]
    public IList<DocumentTax> Taxes { get; set; } = [];

    [BindNever]
    public List<SelectListItem> PaymentTerms { get; set; } = [];

    [BindNever]
    public List<SelectListItem> LineKinds { get; set; } = [];

    [BindNever]
    public string PartySearchEndpoint { get; set; }

    [BindNever]
    public bool CanIssue { get; set; }
}
