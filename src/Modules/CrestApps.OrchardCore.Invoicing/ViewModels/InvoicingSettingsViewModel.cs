using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The invoicing settings form.
/// </summary>
public class InvoicingSettingsViewModel
{
    public string DefaultPaymentTermId { get; set; }

    public string DefaultCurrency { get; set; }

    public string DefaultNotes { get; set; }

    public string DefaultTerms { get; set; }

    public string DefaultFooter { get; set; }

    public bool PricesIncludeTax { get; set; }

    public bool AllowPartialPayment { get; set; }

    public bool IssueOnSend { get; set; }

    public decimal? CashRoundingIncrement { get; set; }

    public int ShareLinkDays { get; set; }

    public IList<SelectListItem> PaymentTerms { get; set; } = [];
}
