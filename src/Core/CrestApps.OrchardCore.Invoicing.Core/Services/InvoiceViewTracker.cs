using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Records the first time the customer opens an issued invoice's public page: <see cref="Invoice.ViewedUtc"/>, the
/// delivery status <see cref="InvoiceDelivery.Viewed"/> and a history entry. Later opens change nothing.
/// </summary>
public sealed class InvoiceViewTracker
{
    private readonly IInvoiceStore _invoices;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public InvoiceViewTracker(IInvoiceStore invoices, ISession session, IClock clock, IStringLocalizer<InvoiceViewTracker> stringLocalizer)
    {
        _invoices = invoices;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Records that the customer opened the invoice. Returns whether this was the first time.
    /// </summary>
    public async Task<bool> RecordAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (invoice.ViewedUtc.HasValue || !invoice.IssuedUtc.HasValue)
        {
            return false;
        }

        var now = _clock.UtcNow;

        invoice.ViewedUtc = now;
        invoice.Delivery = InvoiceLifecycle.AdvanceDelivery(invoice.Delivery, InvoiceDelivery.Viewed);
        invoice.ModifiedUtc = now;
        invoice.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentEventKinds.Viewed,
            CreatedUtc = now,
            Message = S["Opened by the customer for the first time."].Value,
        });

        try
        {
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            // Seen while it was being changed: the next open records it.
            return false;
        }

        return true;
    }
}
