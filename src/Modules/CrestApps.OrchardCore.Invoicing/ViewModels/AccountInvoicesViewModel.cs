using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The Invoices section of an account's page.
/// </summary>
public class AccountInvoicesViewModel
{
    public string BillTo { get; set; }

    public IReadOnlyList<Invoice> Invoices { get; set; } = [];

    public int Count { get; set; }

    public IReadOnlyList<(string Currency, decimal Due, decimal Overdue)> Totals { get; set; } = [];

    public bool CanCreate { get; set; }
}
