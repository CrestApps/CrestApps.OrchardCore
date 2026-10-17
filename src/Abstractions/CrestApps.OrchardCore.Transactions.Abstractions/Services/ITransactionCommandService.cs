using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The actions staff take on a <see cref="Transaction"/>: record an offline payment, mark it paid, cancel it, lower what
/// is owed, send its invoice, and refund it. The Commerce console uses it, so a module that offers the same actions on
/// its own screens gets the same timeline entries, receipts and handler calls
/// (<see cref="ITransactionPaymentHandler"/>, <see cref="IPaymentRefundHandler"/> and <see cref="ITransactionEventHandler"/>).
/// </summary>
/// <remarks>
/// Each command takes a lock on the transaction, loads it, checks that the transaction accepts the command in its
/// current state, and saves it, so two commands on the same transaction never overwrite each other. A refused command
/// returns its reasons in <see cref="TransactionCommandResult.Errors"/> rather than throwing. The service does not check
/// permissions: the caller decides who may run a command. The timeline records the signed-in user, when there is one,
/// as the actor.
/// </remarks>
public interface ITransactionCommandService
{
    /// <summary>
    /// Records money received outside the site, such as cash or a check, against the transaction. An amount above what
    /// is still owed records only what is owed. The transaction becomes paid once nothing is owed, and the payment
    /// handlers are told about the payment.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="amount">The amount received, in the transaction's currency. It must be greater than zero.</param>
    /// <param name="reference">An optional reference for the payment, such as a check number.</param>
    /// <param name="note">An optional note recorded with the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> RecordOfflinePaymentAsync(string transactionId, decimal amount, string reference, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles whatever is still owed on the transaction as paid offline, and tells the payment handlers about that
    /// payment.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="note">An optional note recorded with the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> MarkPaidAsync(string transactionId, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the transaction so nothing more is collected for it. A transaction that was already paid is refunded
    /// instead.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="reason">An optional reason, recorded on the timeline and passed to
    /// <see cref="ITransactionEventHandler.CanceledAsync"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> CancelAsync(string transactionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lowers what is owed on the transaction without money being received: adds <paramref name="amount"/> to
    /// <see cref="Transaction.AmountAdjusted"/> and records a <see cref="TransactionEventType.Adjusted"/> event. An
    /// adjustment can never be more than what is still owed. A transaction that owes nothing after it is settled and is
    /// never reminded again. Adjustments show on the invoice and on the receipts.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="amount">The amount to take off, in the transaction's currency. It must be greater than zero.</param>
    /// <param name="kind">Why the amount is taken off.</param>
    /// <param name="reason">An optional reason recorded with the adjustment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> AdjustAsync(string transactionId, decimal amount, TransactionAdjustmentKind kind, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the owner the transaction's invoice now: issues its invoice number when it has none, and sends an
    /// "Invoice INV-1001" notice with the link that pays it. The notice counts as the first notice, so the reminder
    /// sweep waits the reminder interval before it sends one. Needs the Transaction Reminders feature.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> SendInvoiceAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives money paid online for the transaction back: finds the confirmed payments made for it and refunds them
    /// through the checkout refund service, newest first, until <paramref name="amount"/> is refunded. The refund
    /// handlers then record each refund on the transaction. Needs the Checkout feature.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="amount">The amount to refund, including tax, or <see langword="null"/> to refund everything that can
    /// still be refunded.</param>
    /// <param name="reason">An optional reason passed on to each refund.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> RefundAsync(string transactionId, decimal? amount, string reason, CancellationToken cancellationToken = default);
}
