namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The invoices whose lock this request holds while an invoice command runs. Work the command causes on the ledger, such
/// as mirroring the transaction back onto the invoice, sees the invoice is busy here and skips it without waiting for a
/// lock the same request holds: the command mirrors what it changes itself.
/// </summary>
public sealed class InvoiceCommandLocks
{
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether this request holds the invoice's lock.
    /// </summary>
    public bool IsHeld(string invoiceId)
        => !string.IsNullOrEmpty(invoiceId) && _held.Contains(invoiceId);

    /// <summary>
    /// Marks the invoice's lock held until the returned scope is disposed.
    /// </summary>
    public IDisposable Hold(string invoiceId)
    {
        var added = !string.IsNullOrEmpty(invoiceId) && _held.Add(invoiceId);

        return new Release(this, added ? invoiceId : null);
    }

    private sealed class Release : IDisposable
    {
        private readonly InvoiceCommandLocks _locks;
        private string _invoiceId;

        public Release(InvoiceCommandLocks locks, string invoiceId)
        {
            _locks = locks;
            _invoiceId = invoiceId;
        }

        public void Dispose()
        {
            if (_invoiceId is not null)
            {
                _locks._held.Remove(_invoiceId);
                _invoiceId = null;
            }
        }
    }
}
