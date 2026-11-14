using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Keeps an issued invoice in step with its transaction (<see cref="InvoiceLedgerMirror"/>), once the ledger's change is
/// saved: after the ledger records a payment or takes one back, after the request that changed its status, canceled or
/// refunded it (<see cref="IInvoiceLedgerSyncScheduler"/>), and when the invoice's page is opened, so a change missed
/// while the invoice was busy is caught up.
/// </summary>
/// <remarks>
/// It queues the invoice's update in the session and never saves the session itself: the session may carry the work of
/// the command that called it, which saves it (or the request does), and a conflict there is that work's to report.
/// </remarks>
public sealed class InvoiceLedgerSync
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly ITransactionManager _transactions;
    private readonly IDistributedLock _distributedLock;
    private readonly InvoiceCommandLocks _commandLocks;
    private readonly InvoiceObserverNotifier _notifier;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public InvoiceLedgerSync(
        IInvoiceStore invoices,
        ICreditNoteStore creditNotes,
        ITransactionManager transactions,
        IDistributedLock distributedLock,
        InvoiceCommandLocks commandLocks,
        InvoiceObserverNotifier notifier,
        IClock clock,
        IStringLocalizer<InvoiceLedgerSync> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _transactions = transactions;
        _distributedLock = distributedLock;
        _commandLocks = commandLocks;
        _notifier = notifier;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Mirrors a saved transaction onto the invoice that posted it, if one did.
    /// </summary>
    public async Task SyncAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction is null ||
            !string.Equals(transaction.ReferenceType, InvoicingConstants.InvoiceReferenceType, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return;
        }

        await SyncAsync(transaction.ReferenceId, transaction, cancellationToken);
    }

    /// <summary>
    /// Reads a transaction and mirrors it onto the invoice that posted it, if one did.
    /// </summary>
    public async Task SyncTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(transactionId))
        {
            await SyncAsync(await _transactions.FindByIdAsync(transactionId, cancellationToken), cancellationToken);
        }
    }

    /// <summary>
    /// Mirrors the invoice's transaction onto it. Returns the invoice as it is now.
    /// </summary>
    public async Task<Invoice> SyncAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice is null || invoice.Status == InvoiceStatus.Draft || string.IsNullOrEmpty(invoice.TransactionId))
        {
            return invoice;
        }

        var transaction = await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken);

        return transaction is null ? invoice : await SyncAsync(invoice.ItemId, transaction, cancellationToken) ?? invoice;
    }

    private async Task<Invoice> SyncAsync(string invoiceId, Transaction transaction, CancellationToken cancellationToken)
    {
        // An invoice command of this request holds the invoice: it mirrors what it causes itself, and waiting here for the
        // lock it holds would only time out.
        if (_commandLocks.IsHeld(invoiceId))
        {
            return null;
        }

        // Another request's command holds it: anything missed is caught up when the invoice is next opened.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultInvoiceCommandService.LockPrefix + invoiceId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var _ = locker;

        var invoice = await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null || invoice.Status == InvoiceStatus.Draft ||
            !string.Equals(invoice.TransactionId, transaction.ItemId, StringComparison.Ordinal))
        {
            return invoice;
        }

        var credited = (await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken))
            .Where(creditNote => creditNote.Status == CreditNoteStatus.Issued)
            .Sum(creditNote => creditNote.Totals?.Total ?? 0m);

        var paidBefore = invoice.AmountPaid;
        var settlementBefore = invoice.Settlement;

        if (InvoiceLedgerMirror.Apply(invoice, transaction, credited, S))
        {
            invoice.ModifiedUtc = _clock.UtcNow;

            await _invoices.UpdateAsync(invoice, cancellationToken);

            // The observers hear it once the invoice's update is saved, which this method leaves to its caller.
            var received = invoice.AmountPaid - paidBefore;

            if (received > 0m)
            {
                await _notifier.NotifyAfterSaveAsync(invoice, (observer, saved) => observer.PaymentReceivedAsync(saved, received));
            }

            if (invoice.Settlement == InvoiceSettlement.Paid && settlementBefore != InvoiceSettlement.Paid)
            {
                await _notifier.NotifyAfterSaveAsync(invoice, (observer, saved) => observer.PaidAsync(saved));
            }
        }

        return invoice;
    }
}
