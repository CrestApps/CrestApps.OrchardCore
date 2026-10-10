using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The page a signed pay link opens.
/// </summary>
public class TransactionPayViewModel
{
    /// <summary>
    /// Gets or sets the signed token from the link, posted back to pay.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the transaction the link names.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the transaction's invoice, or <see langword="null"/> when invoices are not available.
    /// </summary>
    public ReceiptDocument Invoice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transaction can be paid online now.
    /// </summary>
    public bool CanPay { get; set; }
}
