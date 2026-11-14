using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Handlers;

/// <summary>
/// Mirrors the ledger onto the invoice once the ledger's change is saved. A payment recorded and a payment taken back are
/// heard after the ledger command saved, so the invoice follows at once. A status change, a cancel and a refund are heard
/// while the command still has the transaction open, so the invoice follows after the request
/// (<see cref="IInvoiceLedgerSyncScheduler"/>): saving it then would commit the command half-way.
/// </summary>
public sealed class InvoiceTransactionHandler : ITransactionPaymentHandler, ITransactionEventHandler
{
    private readonly InvoiceLedgerSync _sync;
    private readonly IInvoiceLedgerSyncScheduler _scheduler;

    public InvoiceTransactionHandler(InvoiceLedgerSync sync, IInvoiceLedgerSyncScheduler scheduler)
    {
        _sync = sync;
        _scheduler = scheduler;
    }

    public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(context?.Transaction, cancellationToken);

    public Task ReversedAsync(Transaction transaction, TransactionEvent reversal, TransactionEvent reversed, CancellationToken cancellationToken = default)
        => _sync.SyncAsync(transaction, cancellationToken);

    public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => ScheduleAsync(transaction);

    public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => ScheduleAsync(transaction);

    public Task RefundedAsync(Transaction transaction, TransactionEvent refund, CancellationToken cancellationToken = default)
        => ScheduleAsync(transaction);

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private Task ScheduleAsync(Transaction transaction)
    {
        if (transaction is not null && string.Equals(transaction.ReferenceType, InvoicingConstants.InvoiceReferenceType, StringComparison.Ordinal))
        {
            _scheduler.Schedule(transaction.ItemId);
        }

        return Task.CompletedTask;
    }
}
