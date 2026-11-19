using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The editor of a late-fee policy.
/// </summary>
public class LateFeePolicyViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool Enabled { get; set; }

    public LateFeeKind Kind { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percent { get; set; }

    public int GraceDays { get; set; }

    public int? RepeatEveryDays { get; set; }

    public int? MaxFees { get; set; }

    public decimal? CapAmount { get; set; }

    public decimal? MinimumBalance { get; set; }

    public bool Taxable { get; set; }

    public bool SendDebitNote { get; set; }
}

/// <summary>
/// The late fees card of an invoice's page.
/// </summary>
public class InvoiceLateFeesViewModel
{
    public Invoice Invoice { get; set; }

    public IReadOnlyList<Invoice> DebitNotes { get; set; } = [];

    public Invoice RelatedInvoice { get; set; }

    public bool CanAssess { get; set; }
}
