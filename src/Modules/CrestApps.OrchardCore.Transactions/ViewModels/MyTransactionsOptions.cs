using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The filter options for the customer statement of their own transactions.
/// </summary>
public class MyTransactionsOptions
{
    /// <summary>
    /// Gets or sets the case-insensitive title search term.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status selection to filter the statement by.
    /// </summary>
    public TransactionStatusFilter Status { get; set; }

    /// <summary>
    /// Gets or sets the status filter items rendered in the statement toolbar.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; }
}
