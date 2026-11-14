using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Keeps an issued invoice in step with its transaction (<see cref="InvoiceLedgerMirror"/>): after the ledger records a
/// payment, takes one back, refunds or settles, and when the invoice's page is opened, so a change missed while the
/// invoice was busy is caught up.
/// </summary>
public sealed class InvoiceLedgerSync
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly ITransactionManager _transactions;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public InvoiceLedgerSync(
        IInvoiceStore invoices,
        ICreditNoteStore creditNotes,
        ITransactionManager transactions,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<InvoiceLedgerSync> logger,
        IStringLocalizer<InvoiceLedgerSync> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _transactions = transactions;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Mirrors a transaction onto the invoice that posted it, if one did.
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
        // Under the invoice's own lock. A command holding it (issue, void, write off) mirrors the ledger itself, so a
        // change it causes is not mirrored twice; anything else missed is caught up when the invoice is next opened.
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

        if (!InvoiceLedgerMirror.Apply(invoice, transaction, credited, S))
        {
            return invoice;
        }

        invoice.ModifiedUtc = _clock.UtcNow;

        try
        {
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            // The ledger is the record: the invoice is mirrored again the next time it is opened or its transaction changes.
            _logger.LogWarning("Invoice '{InvoiceId}' was changed while its payments were being copied from the ledger; they are copied again next time.", invoiceId);
        }

        return invoice;
    }
}
