using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The printable receipt for one payment applied to a transaction. It is the model of the <c>TransactionReceipt</c>
/// shape (<see cref="TransactionsConstants.Shapes.Receipt"/>) and of the admin receipt page.
/// </summary>
public class TransactionReceiptViewModel
{
    /// <summary>
    /// Gets or sets the transaction the payment was applied to.
    /// </summary>
    public Transaction Transaction { get; set; }

    /// <summary>
    /// Gets or sets the timeline event that recorded the payment.
    /// </summary>
    public TransactionEvent Payment { get; set; }

    /// <summary>
    /// Gets or sets the receipt.
    /// </summary>
    public ReceiptDocument Receipt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the viewer manages transactions, which decides where "back" leads.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Copies every value of this model to <paramref name="target"/>, for example to initialize the
    /// <see cref="TransactionsConstants.Shapes.Receipt"/> shape from a model built by
    /// <see cref="Services.ITransactionCustomerPageService"/>.
    /// </summary>
    /// <param name="target">The model to copy to.</param>
    public void CopyTo(TransactionReceiptViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Transaction = Transaction;
        target.Payment = Payment;
        target.Receipt = Receipt;
        target.IsAdministrator = IsAdministrator;
    }
}
