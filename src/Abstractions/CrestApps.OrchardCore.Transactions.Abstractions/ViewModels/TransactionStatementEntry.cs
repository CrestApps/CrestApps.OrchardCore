using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// One transaction on a customer's statement, with the links the customer can follow from it.
/// </summary>
public class TransactionStatementEntry
{
    /// <summary>
    /// Gets or sets the transaction.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the signed link that pays the transaction, or <see langword="null"/> when nothing can be paid on
    /// it or the site's address is not known.
    /// </summary>
    public string PayUrl { get; set; }

    /// <summary>
    /// Gets or sets the address of the transaction's invoice page in the site theme, or <see langword="null"/> when the
    /// transaction has no invoice or invoices are not available.
    /// </summary>
    public string InvoiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the receipts of the payments applied to the transaction, oldest first. It is empty when receipts
    /// are not available.
    /// </summary>
    public IReadOnlyList<TransactionStatementReceipt> Receipts { get; set; } = [];
}
