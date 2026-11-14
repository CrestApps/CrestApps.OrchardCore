using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Handlers;

/// <summary>
/// Mirrors the ledger onto the invoice whenever the transaction an invoice posted records a payment, takes one back,
/// refunds, is canceled or changes status (settled by a payment or an adjustment).
/// </summary>
public sealed class InvoiceTransactionHandler : ITransactionPaymentHandler, ITransactionEventHandler
{
    private readonly InvoiceLedgerSync _sync;

    public InvoiceTransactionHandler(InvoiceLedgerSync sync)
    {
        _sync = sync;
    }

    public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(context?.Transaction, cancellationToken);

    public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(transaction, cancellationToken);

    public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(transaction, cancellationToken);

    public Task RefundedAsync(Transaction transaction, TransactionEvent refund, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(transaction, cancellationToken);

    public Task ReversedAsync(Transaction transaction, TransactionEvent reversal, TransactionEvent reversed, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(transaction, cancellationToken);

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
