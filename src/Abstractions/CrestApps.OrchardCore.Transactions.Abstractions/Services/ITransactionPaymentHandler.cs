using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Reacts to money being applied to a <see cref="Transaction"/>, however it was paid: settled online through the
/// checkout, recorded by hand as an offline payment, or marked paid by an administrator. It is the one place to
/// send a receipt, advance whatever the transaction belongs to, or notify someone, so none of that depends on
/// which screen or process took the payment.
/// </summary>
/// <remarks>
/// Handlers run after the payment is applied to the transaction and saved. A handler that fails is logged and
/// does not undo the payment or stop the other handlers.
/// </remarks>
public interface ITransactionPaymentHandler
{
    /// <summary>
    /// Called after a payment was applied to a transaction.
    /// </summary>
    /// <param name="context">The transaction and the payment applied to it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The payment a <see cref="ITransactionPaymentHandler"/> is told about.
/// </summary>
public sealed class TransactionPaymentRecordedContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPaymentRecordedContext"/> class.
    /// </summary>
    /// <param name="transaction">The transaction, as saved with the payment applied.</param>
    /// <param name="payment">The timeline event that recorded the payment.</param>
    public TransactionPaymentRecordedContext(Transaction transaction, TransactionEvent payment)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(payment);

        Transaction = transaction;
        Payment = payment;
    }

    /// <summary>
    /// Gets the transaction, as saved with the payment applied.
    /// </summary>
    public Transaction Transaction { get; }

    /// <summary>
    /// Gets the timeline event that recorded the payment. Its <see cref="TransactionEvent.Amount"/> is the money
    /// applied, its <see cref="TransactionEvent.Method"/> says how, and its
    /// <see cref="TransactionEvent.PaymentAttemptId"/> names the ledger attempt when it was paid online.
    /// </summary>
    public TransactionEvent Payment { get; }
}
