using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The actions staff take on a <see cref="Transaction"/>: record an offline payment, mark it paid, cancel it, lower what
/// is owed, send its invoice, refund it, and charge it to a card its owner kept. The Commerce console uses it, so a module that offers the same actions on
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
    /// Records money received outside the site against the transaction, with how and when it was received. An amount
    /// above what is still owed records only what is owed, and the rest is returned in
    /// <see cref="TransactionCommandResult.UnappliedAmount"/>. The transaction becomes paid once nothing is owed, and
    /// the payment handlers are told about the payment.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="request">The payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> RecordPaymentAsync(string transactionId, TransactionPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pays toward the transaction with its payer's customer credit (<c>ICustomerCreditService</c>, the Customers Store
    /// Credit feature): spends <paramref name="amount"/>, or what is still owed when that is less, from the credit and
    /// records it as a payment, so the receipts and the payment handlers see it like any other. It is refused when the
    /// credit is less than the amount, the payer can't hold credit, or the feature is off. Taking the payment back gives
    /// the credit back.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="amount">The amount of credit to apply, in the transaction's currency. It must be greater than zero.</param>
    /// <param name="note">An optional note recorded with the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> ApplyCreditAsync(string transactionId, decimal amount, string note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the customer credit changes a transaction's timeline entries are waiting for: each entry is saved first,
    /// as the intent, and its credit change is made after it, keyed by the entry's id so it is made once. A process that
    /// stopped between the two left the entry waiting; every command completes such entries before it runs, and the
    /// reminder sweep calls this for the transactions that have any. A spend that finds too little credit takes its
    /// payment back with a reversing entry; a change that cannot be made is recorded as a payment issue.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> ReconcileCreditAsync(string transactionId, CancellationToken cancellationToken = default);

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
    /// Cancels the transaction only when nothing was paid toward it, decided under the transaction's lock, so a payment
    /// recorded a moment earlier is never canceled along with it: then the command is refused. A document that voids
    /// the debt it posted, such as an invoice, uses it. A transaction already canceled is left as it is and reported as
    /// canceled, so repeating the command is safe.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="reason">An optional reason, recorded on the timeline.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> CancelUnpaidAsync(string transactionId, string reason, CancellationToken cancellationToken = default);

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
    /// Lowers what is owed on the transaction without money being received, for a document such as a credit note. It
    /// does what <see cref="AdjustAsync(string, decimal, TransactionAdjustmentKind, string, CancellationToken)"/> does,
    /// names the document on the timeline, and records nothing when an adjustment for the same document is already
    /// there.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="request">The adjustment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> AdjustAsync(string transactionId, TransactionAdjustmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back a payment recorded by hand, or an adjustment, that should not have been recorded. The entry stays on
    /// the timeline; an entry that reverses it is added, its amount is owed again, and a transaction that was settled
    /// by it is collectable again. A payment taken online is not reversed this way: it is refunded.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="eventId">The identifier of the timeline entry to take back.</param>
    /// <param name="reason">Why it is taken back.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> ReverseAsync(string transactionId, string eventId, string reason, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Charges what is still owed on the transaction to a payment method its owner kept, without the owner present, for
    /// example to renew a subscription charged to the card on file. The charge runs through the checkout like any other
    /// payment, so a payment that goes through is recorded on the transaction, receipted and reported to the payment and
    /// lifecycle handlers exactly as a checkout payment is. A charge that is not taken is recorded on the timeline as a
    /// <see cref="TransactionEventType.PaymentFailed"/> event, and <see cref="TransactionCommandResult.ChargeStatus"/>
    /// tells the caller what to do next: for example, send the invoice when the card was declined or needs the
    /// cardholder to approve the payment. Needs the Checkout feature and a payment provider that can charge saved
    /// payment methods.
    /// </summary>
    /// <remarks>
    /// The payment method must belong to the transaction's owner (<c>ISavedPaymentMethodStore</c> in the checkout
    /// abstractions lists an owner's payment methods). A charge already handed to the provider for this transaction is
    /// finished rather than repeated, so calling the command again never charges the owner twice. Only server code may
    /// call it: it spends a card without its owner present.
    /// </remarks>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="savedPaymentMethodId">The identifier of the owner's kept payment method to charge.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> ChargeSavedPaymentMethodAsync(string transactionId, string savedPaymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges what a transaction owes to the card a checkout kept, such as the card an installment plan's first payment or
    /// a booking's deposit was paid with, as <see cref="ChargeSavedPaymentMethodAsync"/> charges a saved card: one charge
    /// at a time, a charge already handed to the provider finished rather than repeated, and money a charge took but did
    /// not record applied first. The checkout must belong to the transaction's owner (or be a guest's for a guest's
    /// transaction). Only server code that recorded the checkout itself calls it.
    /// </summary>
    /// <param name="transactionId">The transaction to charge.</param>
    /// <param name="sourceSessionId">The checkout that kept the card.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> ChargeKeptPaymentMethodAsync(string transactionId, string sourceSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses or resumes the scheduled reminders of a transaction, and notes it on its timeline. A paused transaction
    /// gets no reminder from the sweep until it is resumed; the reminders sent by hand still go.
    /// </summary>
    /// <param name="transactionId">The transaction identifier.</param>
    /// <param name="paused">Whether its reminders are paused.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TransactionCommandResult> SetRemindersPausedAsync(string transactionId, bool paused, CancellationToken cancellationToken = default);
}
