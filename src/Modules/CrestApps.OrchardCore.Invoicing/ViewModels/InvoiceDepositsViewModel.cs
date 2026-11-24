using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The deposits card of an invoice's page.
/// </summary>
public class InvoiceDepositsViewModel
{
    public Invoice Invoice { get; set; }

    public bool CanManage { get; set; }

    public IReadOnlyList<Invoice> Deductible { get; set; } = [];

    public IReadOnlyList<Invoice> Requested { get; set; } = [];

    public Invoice DepositFor { get; set; }
}
