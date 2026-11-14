using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The customer's invoices, a page at a time.
/// </summary>
public class MyInvoicesViewModel
{
    public IReadOnlyList<Invoice> Invoices { get; set; } = [];

    public IReadOnlySet<string> Payable { get; set; } = new HashSet<string>();

    public int Count { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
