using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The filter options for the administration transactions report.
/// </summary>
public class TransactionsAdminIndexOptions
{
    /// <summary>
    /// Gets or sets the case-insensitive title search term.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status selection to filter the report by.
    /// </summary>
    public TransactionStatusFilter Status { get; set; }

    /// <summary>
    /// Gets or sets the origin/provider key to filter the report by.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the status filter items rendered in the report toolbar.
    /// </summary>
    public List<SelectListItem> Statuses { get; set; }
}
