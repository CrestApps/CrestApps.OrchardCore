using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The pages a share link opens without signing in: the invoice with its pay button, and its PDF. The first open by
/// someone who is not staff marks the invoice viewed.
/// </summary>
[AllowAnonymous]
public sealed class InvoiceController : Controller
{
    private readonly IInvoiceShareLinkService _shareLinks;
    private readonly IInvoiceStore _invoices;
    private readonly IAuthorizationService _authorizationService;

    public InvoiceController(IInvoiceShareLinkService shareLinks, IInvoiceStore invoices, IAuthorizationService authorizationService)
    {
        _shareLinks = shareLinks;
        _invoices = invoices;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Shows the invoice a share link names.
    /// </summary>
    public async Task<IActionResult> Show(string token, [FromServices] IInvoiceDocumentBuilder builder, [FromServices] InvoiceViewTracker tracker)
    {
        var invoice = await FindAsync(token);

        if (invoice is null)
        {
            return View("Invalid");
        }

        // Staff opening the link to check it don't count as the customer having seen it.
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            await tracker.RecordAsync(invoice, HttpContext.RequestAborted);
        }

        string payUrl = null;

        if (await HttpContext.RequestServices.GetRequiredService<InvoiceLedgerAvailability>().CanPayAsync(invoice, HttpContext.RequestAborted) &&
            HttpContext.RequestServices.GetService<ITransactionPayLinkService>() is { } payLinks &&
            await HttpContext.RequestServices.GetRequiredService<ITransactionManager>().FindByIdAsync(invoice.TransactionId, HttpContext.RequestAborted) is { } transaction)
        {
            payUrl = await payLinks.CreatePayUrlAsync(transaction, HttpContext.RequestAborted);
        }

        return View(new InvoiceDocumentPageViewModel
        {
            Source = invoice,
            Document = await builder.BuildAsync(invoice, HttpContext.RequestAborted),
            PdfUrl = Url.RouteUrl(InvoicingConstants.RouteNames.ViewPdf, new { token }),
            PayUrl = payUrl,
        });
    }

    /// <summary>
    /// Downloads the PDF of the invoice a share link names.
    /// </summary>
    public async Task<IActionResult> Pdf(string token, [FromServices] IInvoicePdfService pdfService)
    {
        var invoice = await FindAsync(token);

        if (invoice is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(invoice, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    // Only an issued invoice is shown by link: a draft is not the customer's to see.
    private async Task<Invoice> FindAsync(string token)
    {
        var invoiceId = _shareLinks.GetInvoiceId(token);

        if (string.IsNullOrEmpty(invoiceId))
        {
            return null;
        }

        var invoice = await _invoices.FindByIdAsync(invoiceId, HttpContext.RequestAborted);

        return invoice?.IssuedUtc is null ? null : invoice;
    }
}
