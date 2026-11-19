using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Webhooks;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Tells webhooks what happens to invoices. Each delivery carries a stable event id built from the document and what
/// happened, so a receiver can drop a repeat. Amounts are numbers in the document's currency, dates are ISO dates and
/// times are UTC.
/// </summary>
internal sealed class InvoiceWebhookObserver : IInvoiceObserver
{
    private readonly IWebhookPublisher _publisher;

    public InvoiceWebhookObserver(IWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task CreatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceCreated, invoice, null, $"evt_invoice-created_{invoice.ItemId}", cancellationToken);

    public Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceIssued, invoice, null, $"evt_invoice-issued_{invoice.ItemId}", cancellationToken);

    public Task SentAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceSent, invoice, null, $"evt_invoice-sent_{invoice.ItemId}_{Ticks(invoice)}", cancellationToken);

    public Task ViewedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceViewed, invoice, null, $"evt_invoice-viewed_{invoice.ItemId}", cancellationToken);

    public Task PaymentReceivedAsync(Invoice invoice, decimal amount, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoicePaymentReceived, invoice, new { amount }, $"evt_invoice-payment_{invoice.ItemId}_{Ticks(invoice)}", cancellationToken);

    public Task PaidAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoicePaid, invoice, null, $"evt_invoice-paid_{invoice.ItemId}_{Ticks(invoice)}", cancellationToken);

    public Task OverdueAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceOverdue, invoice, null, $"evt_invoice-overdue_{invoice.ItemId}", cancellationToken);

    public Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.InvoiceVoided, invoice, new { reason = invoice.VoidReason }, $"evt_invoice-voided_{invoice.ItemId}", cancellationToken);

    public Task CreditNoteIssuedAsync(CreditNote creditNote, Invoice invoice, CancellationToken cancellationToken = default)
        => PublishAsync(InvoicingConstants.WebhookEvents.CreditNoteIssued, invoice, new
        {
            creditNote = new
            {
                id = creditNote.ItemId,
                number = creditNote.Number,
                currency = creditNote.Currency,
                total = creditNote.Totals?.Total ?? 0m,
                disposition = creditNote.Disposition.ToString(),
                reason = creditNote.Reason,
                issueDate = creditNote.IssueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            },
        }, $"evt_credit-note-issued_{creditNote.ItemId}", cancellationToken);

    private static long Ticks(Invoice invoice)
        => (invoice.ModifiedUtc ?? invoice.CreatedUtc).Ticks;

    private Task PublishAsync(string eventName, Invoice invoice, object details, string eventId, CancellationToken cancellationToken)
    {
        var party = invoice.BillTo?.Party ?? default;

        return _publisher.PublishAsync(eventName, new
        {
            invoice = new
            {
                id = invoice.ItemId,
                number = invoice.Number,
                kind = invoice.Kind.ToString(),
                status = invoice.Status.ToString(),
                settlement = invoice.Settlement.ToString(),
                delivery = invoice.Delivery.ToString(),
                currency = invoice.Currency,
                subtotal = invoice.Totals?.Subtotal ?? 0m,
                taxTotal = invoice.Totals?.TaxTotal ?? 0m,
                total = invoice.Totals?.Total ?? 0m,
                amountPaid = invoice.AmountPaid,
                amountCredited = invoice.AmountCredited,
                amountDue = invoice.AmountDue,
                billToKind = party.IsEmpty ? null : party.Kind,
                billToId = party.IsEmpty ? null : party.Id,
                billToName = invoice.BillTo?.Contact?.Name,
                issueDate = invoice.IssueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                dueDate = invoice.DueDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                transactionId = invoice.TransactionId,
                sourceType = invoice.SourceType,
                sourceId = invoice.SourceId,
                createdUtc = invoice.CreatedUtc,
                issuedUtc = invoice.IssuedUtc,
            },
            details,
        }, eventId, cancellationToken);
    }
}
