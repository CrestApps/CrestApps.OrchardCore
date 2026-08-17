namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The view model for the administration transactions report.
/// </summary>
public class TransactionsAdminIndexViewModel
{
    /// <summary>
    /// Gets or sets the transactions shown on the current page.
    /// </summary>
    public IList<TransactionListItemViewModel> Transactions { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter options applied to the report.
    /// </summary>
    public TransactionsAdminIndexOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the total outstanding amount across every transaction that matches the current filter.
    /// </summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered under the report.
    /// </summary>
    public dynamic Pager { get; set; }
}
