using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.Logging;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Handlers;

/// <summary>
/// Stores the PDF of an invoice once it is issued (<see cref="IInvoicePdfService.ArchiveAsync"/>). A failure is logged
/// and leaves the invoice without an archive; its PDF is then rendered from what it keeps until it is archived.
/// </summary>
public sealed class InvoiceArchiveObserver : IInvoiceObserver
{
    private readonly IInvoicePdfService _pdf;
    private readonly IInvoiceStore _invoices;
    private readonly ISession _session;
    private readonly ILogger _logger;

    public InvoiceArchiveObserver(IInvoicePdfService pdf, IInvoiceStore invoices, ISession session, ILogger<InvoiceArchiveObserver> logger)
    {
        _pdf = pdf;
        _invoices = invoices;
        _session = session;
        _logger = logger;
    }

    public async Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _pdf.ArchiveAsync(invoice, cancellationToken))
            {
                await _invoices.UpdateAsync(invoice, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The PDF of invoice '{InvoiceId}' could not be archived.", invoice.ItemId);
        }
    }
}
