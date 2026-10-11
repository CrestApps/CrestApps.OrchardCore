using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// A single row in the administration transactions report.
/// </summary>
public class TransactionListItemViewModel
{
    /// <summary>
    /// Gets or sets the transaction this row represents.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the display name of the transaction owner, when it could be resolved.
    /// </summary>
    public string OwnerName { get; set; }
}
