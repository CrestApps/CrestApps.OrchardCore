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
    /// Gets or sets a value indicating whether only outstanding transactions are shown.
    /// </summary>
    public bool OutstandingOnly { get; set; }

    /// <summary>
    /// Gets or sets the total amount the customer still owes across every outstanding transaction.
    /// </summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="TotalOutstanding"/>, when a single currency applies.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered under the statement.
    /// </summary>
    public dynamic Pager { get; set; }
}
