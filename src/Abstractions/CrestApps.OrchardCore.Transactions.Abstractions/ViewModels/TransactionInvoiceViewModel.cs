using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The printable invoice of a transaction. It is the model of the <c>TransactionInvoice</c> shape
/// (<see cref="TransactionsConstants.Shapes.Invoice"/>) and of the admin invoice page.
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
    /// Gets or sets the signed pay link of the transaction, while the invoice is still due. An administrator can copy
    /// it to send it to the customer; the customer follows it to pay.
    /// </summary>
    public string PayUrl { get; set; }

    /// <summary>
    /// Copies every value of this model to <paramref name="target"/>, for example to initialize the
    /// <see cref="TransactionsConstants.Shapes.Invoice"/> shape from a model built by
    /// <see cref="Services.ITransactionCustomerPageService"/>.
    /// </summary>
    /// <param name="target">The model to copy to.</param>
    public void CopyTo(TransactionInvoiceViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Transaction = Transaction;
        target.Invoice = Invoice;
        target.IsAdministrator = IsAdministrator;
        target.PayUrl = PayUrl;
    }
}
