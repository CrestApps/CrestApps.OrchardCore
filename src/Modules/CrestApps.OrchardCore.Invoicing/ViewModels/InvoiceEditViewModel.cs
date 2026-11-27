using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The draft invoice editor.
/// </summary>
public class InvoiceEditViewModel
{
    /// <summary>
    /// Gets or sets the editor of the page, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets who the invoice is billed to, as a party's <c>Kind:Id</c>, or empty for someone known only by the
    /// contact typed.
    /// </summary>
    public string BillTo { get; set; }

    public string ContactName { get; set; }

    public string ContactEmail { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the date the invoice is issued with; today when empty.
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    public string PaymentTermId { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public string Reference { get; set; }

    public bool PricesIncludeTax { get; set; }

    public bool AllowPartialPayment { get; set; }

    public bool AutoCharge { get; set; }

    public bool LateFeesExempt { get; set; }

    /// <summary>
    /// Gets or sets the rows of the payment schedule, with a few empty ones to add more.
    /// </summary>
    public List<InvoiceScheduleRowViewModel> Schedule { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a payment schedule can be set (Installment Plans is on).
    /// </summary>
    public bool CanSchedule { get; set; }

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

/// <summary>
/// A row of the invoice editor's payment schedule.
/// </summary>
public class InvoiceScheduleRowViewModel
{
    public DateOnly? DueDate { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percent { get; set; }
}
