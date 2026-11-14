using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Hears what happens to invoices, after it is saved: workflows, webhooks, the archive and other modules react through
/// it, so each stays optional. Every member has a default that does nothing, so an observer implements only what it needs.
/// </summary>
public interface IInvoiceObserver
{
    /// <summary>
    /// Called after an invoice is issued.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an invoice is voided.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an invoice is written off as uncollectible.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task WrittenOffAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an uncollectible invoice is reinstated.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReinstatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
