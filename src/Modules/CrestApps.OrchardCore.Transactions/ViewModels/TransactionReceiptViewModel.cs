using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The printable receipt for one payment applied to a transaction.
/// </summary>
public class TransactionReceiptViewModel
{
    /// <summary>
    /// Gets or sets the transaction the payment was applied to.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the receipt.
    /// </summary>
    public ReceiptDocument Receipt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer manages transactions, which decides where "back" leads.
    /// </summary>
    public bool IsAdministrator { get; set; }
}
