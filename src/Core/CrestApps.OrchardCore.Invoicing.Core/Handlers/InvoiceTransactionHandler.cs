using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Handlers;

/// <summary>
/// Mirrors the ledger onto the invoice once the ledger's change is saved. A payment recorded and a payment taken back are
/// heard after the ledger command saved, so the invoice follows at once. A status change, a cancel and a refund are heard
/// while the command still has the transaction open, so the invoice follows after the request
/// (<see cref="IInvoiceLedgerSyncScheduler"/>): saving it then would commit the command half-way. A debt the ledger finds
/// overdue makes its open invoice overdue for the invoice observers.
/// </summary>
public sealed class InvoiceTransactionHandler : ITransactionPaymentHandler, ITransactionEventHandler
{
    private readonly InvoiceLedgerSync _sync;
    private readonly IInvoiceLedgerSyncScheduler _scheduler;
    private readonly IInvoiceStore _invoices;
    private readonly InvoiceObserverNotifier _notifier;

    public InvoiceTransactionHandler(
        InvoiceLedgerSync sync,
        IInvoiceLedgerSyncScheduler scheduler,
        IInvoiceStore invoices,
        InvoiceObserverNotifier notifier)
    {
        _sync = sync;
        _scheduler = scheduler;
        _invoices = invoices;
        _notifier = notifier;
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

    // The ledger finds a debt overdue once; the invoice that posted it is overdue with it while it is open.
    public async Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (await InvoiceTransactionLink.FindInvoiceAsync(_invoices, transaction, cancellationToken) is not { Status: InvoiceStatus.Open } invoice)
        {
            return;
        }

        await _notifier.NotifyAfterSaveAsync(invoice, (observer, saved) => saved.Status == InvoiceStatus.Open
            ? observer.OverdueAsync(saved)
            : Task.CompletedTask);
    }

    private Task ScheduleAsync(Transaction transaction)
    {
        // Any transaction: the mirror finds the invoice that posted it or took it over, and does nothing for the others.
        if (transaction is not null)
        {
            _scheduler.Schedule(transaction.ItemId);
        }

        return Task.CompletedTask;
    }
}
