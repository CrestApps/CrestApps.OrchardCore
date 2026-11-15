using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The customer's invoices, a page at a time.
/// </summary>
public class MyInvoicesViewModel
{
    public IReadOnlyList<Invoice> Invoices { get; set; } = [];

    public IReadOnlySet<string> Payable { get; set; } = new HashSet<string>();

    /// <summary>
    /// Gets or sets the invoices still owed that are paid through a page of their own, such as their order's checkout,
    /// by invoice id, with that page's address. They are never paid with other invoices.
    /// </summary>
    public IReadOnlyDictionary<string, string> PaidElsewhere { get; set; } = new Dictionary<string, string>();

    public int Count { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
