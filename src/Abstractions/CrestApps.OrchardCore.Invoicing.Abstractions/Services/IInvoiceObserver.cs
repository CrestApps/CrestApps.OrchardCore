using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Hears what happens to invoices, after it is saved: workflows, webhooks, the archive and other modules react through
/// it, so each stays optional. Every member has a default that does nothing, so an observer implements only what it needs.
/// </summary>
/// <remarks>
/// An observer may be called while the command that caused the change still holds the invoice's lock. An observer that
/// runs another invoice command, or anything slow, defers it until the request ends (as the workflow and webhook
/// observers do).
/// </remarks>
public interface IInvoiceObserver
{
    /// <summary>
    /// Called after a draft invoice is created.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an invoice is issued.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an invoice is sent to its customer.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task SentAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called the first time the customer opens an invoice's page.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ViewedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when a payment toward an invoice reaches it from the ledger.
    /// </summary>
    /// <param name="invoice">The invoice, with the payment counted in <see cref="Invoice.AmountPaid"/>.</param>
    /// <param name="amount">The amount the payment added.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task PaymentReceivedAsync(Invoice invoice, decimal amount, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called when nothing more is owed on an invoice because it was paid.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task PaidAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called the first time an invoice is found unpaid after its due date.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task OverdueAsync(Invoice invoice, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Called after a credit note for an invoice is issued.
    /// </summary>
    /// <param name="creditNote">The credit note.</param>
    /// <param name="invoice">The invoice it credits, as it is after the credit.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreditNoteIssuedAsync(CreditNote creditNote, Invoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
