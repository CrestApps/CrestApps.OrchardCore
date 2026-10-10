using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The printable invoice of a transaction.
/// </summary>
public class TransactionInvoiceViewModel
{
    /// <summary>
    /// Gets or sets the transaction.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the invoice.
    /// </summary>
    public ReceiptDocument Invoice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer manages transactions.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Gets or sets the signed pay link an administrator can send the customer, while the invoice is still due.
    /// </summary>
    public string PayUrl { get; set; }
}
