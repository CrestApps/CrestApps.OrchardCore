using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model for the administration transactions report.
/// </summary>
public class TransactionsAdminIndexViewModel
{
    /// <summary>
    /// Gets or sets the filter options applied to the report.
    /// </summary>
    public TransactionsAdminIndexOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the transactions of the current page, with the toolbar filters and the pager, rendered by
    /// the admin list in the layout the site or the user picked.
    /// </summary>
    public AdminListContext List { get; set; }
}
