using System.Collections.Concurrent;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Says whether an issued invoice's transaction is on this site, and so whether it can be paid here. An invoice brought
/// from another site without its transaction keeps its number and amounts, but nothing can be paid, reminded or
/// mirrored until the transaction is imported: staff are told, customers see no way to pay, and it is logged once per
/// transaction. Nothing here throws for a missing transaction.
/// </summary>
public sealed class InvoiceLedgerAvailability
{
    private static readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.Ordinal);

    private readonly ITransactionManager _transactions;
    private readonly ILogger _logger;

    public InvoiceLedgerAvailability(ITransactionManager transactions, ILogger<InvoiceLedgerAvailability> logger)
    {
        _transactions = transactions;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether an issued invoice names a transaction this site does not have.
    /// </summary>
    public async Task<bool> IsMissingAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice is null || invoice.Status == InvoiceStatus.Draft || string.IsNullOrEmpty(invoice.TransactionId))
        {
            return false;
        }

        if (await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is not null)
        {
            return false;
        }

        if (_reported.TryAdd(invoice.TransactionId, 0))
        {
            _logger.LogWarning(
                "Invoice '{InvoiceId}' names transaction '{TransactionId}', which is not on this site, so it can't be paid here. Import the transaction with the Transactions recipe step.",
                invoice.ItemId,
                invoice.TransactionId);
        }

        return true;
    }

    /// <summary>
    /// Gets whether a customer can pay the invoice on this site: it is open, still owes something, and its transaction
    /// is here.
    /// </summary>
    public async Task<bool> CanPayAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => invoice is { Status: InvoiceStatus.Open, AmountDue: > 0m } &&
            !string.IsNullOrEmpty(invoice.TransactionId) &&
            !await IsMissingAsync(invoice, cancellationToken);

    /// <summary>
    /// Returns the ids of the invoices a customer can pay on this site.
    /// </summary>
    public async Task<IReadOnlySet<string>> PayableAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        var payable = new HashSet<string>(StringComparer.Ordinal);

        foreach (var invoice in invoices ?? [])
        {
            if (await CanPayAsync(invoice, cancellationToken))
            {
                payable.Add(invoice.ItemId);
            }
        }

        return payable;
    }
}
