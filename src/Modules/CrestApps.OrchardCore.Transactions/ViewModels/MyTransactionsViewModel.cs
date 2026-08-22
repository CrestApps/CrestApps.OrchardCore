using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model for the customer statement of their own transactions.
/// </summary>
public class MyTransactionsViewModel
{
    /// <summary>
    /// Gets or sets the transactions shown on the current page.
    /// </summary>
    public IReadOnlyCollection<Transaction> Transactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter options applied to the statement.
    /// </summary>
    public MyTransactionsOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the pager shape rendered under the statement.
    /// </summary>
    public dynamic Pager { get; set; }
}

