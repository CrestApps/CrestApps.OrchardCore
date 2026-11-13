using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Webhooks;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Tells webhooks when a transaction is created, paid, settled, becomes overdue, is canceled or refunded. Each delivery
/// carries a stable event id built from the transaction and what happened, so a receiver can drop a repeat.
/// </summary>
internal sealed class TransactionWebhookEventHandler : TransactionEventHandlerBase, ITransactionPaymentHandler
{
    private readonly IWebhookPublisher _publisher;

    public TransactionWebhookEventHandler(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public override Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => PublishAsync(TransactionsConstants.WebhookEvents.Created, transaction, entry: null, $"evt_transaction-created_{transaction.ItemId}", cancellationToken);

    public override Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => current == TransactionStatus.Paid
            ? PublishAsync(TransactionsConstants.WebhookEvents.Settled, transaction, entry: null, $"evt_transaction-settled_{transaction.ItemId}_{transaction.UpdatedUtc.Ticks}", cancellationToken)
            : Task.CompletedTask;

    public override Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => PublishAsync(TransactionsConstants.WebhookEvents.Canceled, transaction, entry: null, $"evt_transaction-canceled_{transaction.ItemId}", cancellationToken);

    public override Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => PublishAsync(TransactionsConstants.WebhookEvents.Overdue, transaction, entry: null, $"evt_transaction-overdue_{transaction.ItemId}", cancellationToken);

    public override Task RefundedAsync(Transaction transaction, TransactionEvent refund, CancellationToken cancellationToken = default)
        => PublishAsync(TransactionsConstants.WebhookEvents.Refunded, transaction, refund, $"evt_transaction-refunded_{transaction.ItemId}_{refund?.Id}", cancellationToken);

    public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
        => PublishAsync(TransactionsConstants.WebhookEvents.PaymentRecorded, context.Transaction, context.Payment, $"evt_transaction-payment_{context.Transaction.ItemId}_{context.Payment.Id}", cancellationToken);

    private Task PublishAsync(string eventName, Transaction transaction, TransactionEvent entry, string eventId, CancellationToken cancellationToken)
    {
        var payer = transaction.GetPayer();

        return _publisher.PublishAsync(eventName, new
        {
            transaction = new
            {
                id = transaction.ItemId,
                title = transaction.Title,
                source = transaction.Source,
                status = transaction.Status.ToString(),
                currency = transaction.Currency,
                totalAmount = transaction.TotalAmount,
                taxAmount = transaction.TaxAmount,
                amountPaid = transaction.AmountPaid,
                amountAdjusted = transaction.AmountAdjusted,
                amountRefunded = transaction.AmountRefunded,
                outstandingAmount = transaction.OutstandingAmount,
                payerKind = payer.IsEmpty ? null : payer.Kind,
                payerId = payer.IsEmpty ? null : payer.Id,
                referenceType = transaction.ReferenceType,
                referenceId = transaction.ReferenceId,
                invoiceNumber = transaction.InvoiceNumber,
                dueUtc = transaction.DueUtc,
                createdUtc = transaction.CreatedUtc,
            },
            entry = entry is null
                ? null
                : new
                {
                    id = entry.Id,
                    type = entry.Type.ToString(),
                    amount = entry.Amount,
                    method = entry.Method,
                    paymentMethod = entry.PaymentMethod,
                    reference = entry.Reference,
                    createdUtc = entry.CreatedUtc,
                },
        }, eventId, cancellationToken);
    }
}
