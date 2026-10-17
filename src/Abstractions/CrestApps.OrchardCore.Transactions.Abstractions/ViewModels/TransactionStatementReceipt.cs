using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The receipt of one payment listed on a customer's statement.
/// </summary>
public class TransactionStatementReceipt
{
    /// <summary>
    /// Gets or sets the timeline event that recorded the payment.
    /// </summary>
    public TransactionEvent Payment { get; set; }

    /// <summary>
    /// Gets or sets the address of the receipt page in the site theme.
    /// </summary>
    public string Url { get; set; }
}
