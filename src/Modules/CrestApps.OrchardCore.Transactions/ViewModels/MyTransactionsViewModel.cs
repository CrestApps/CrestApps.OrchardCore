using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model for the customer statement of their own transactions.
/// </summary>
public class MyTransactionsViewModel
{
    /// <summary>
    /// Gets or sets the filter options applied to the statement.
    /// </summary>
    public MyTransactionsOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the transactions of the current page, with the toolbar filter and the pager, rendered by
    /// the admin list in the layout the site or the user picked.
    /// </summary>
    public AdminListContext List { get; set; }
}
