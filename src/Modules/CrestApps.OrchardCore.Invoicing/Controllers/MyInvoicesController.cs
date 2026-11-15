using System.Security.Claims;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The customer's invoices in their account: those billed to them, to their customer record, or to an account they are an
/// admin of. They open, download, pay one, or pay several together.
/// </summary>
public sealed class MyInvoicesController : Controller
{
    private const int _pageSize = 25;

    private readonly IInvoiceVisibilityService _visibility;
    private readonly IInvoiceManager _invoices;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public MyInvoicesController(
        IInvoiceVisibilityService visibility,
        IInvoiceManager invoices,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<MyInvoicesController> htmlLocalizer)
    {
        _visibility = visibility;
        _invoices = invoices;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the customer's invoices, newest first.
    /// </summary>
    [HttpGet("my-invoices", Name = InvoicingConstants.RouteNames.MyInvoices)]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (await GetUserIdAsync() is not { } userId)
        {
            return Forbid();
        }

        var query = await _visibility.CreateQueryAsync(userId, HttpContext.RequestAborted);
        var result = await _invoices.PageAsync(Math.Max(1, page), _pageSize, query, HttpContext.RequestAborted);

        var invoices = result.Entries.ToList();

        var payable = await HttpContext.RequestServices.GetRequiredService<InvoiceLedgerAvailability>().PayableAsync(invoices, HttpContext.RequestAborted);
        var paidElsewhere = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var invoice in invoices.Where(invoice => payable.Contains(invoice.ItemId)))
        {
            if (await GetOwnPayUrlAsync(invoice) is { } payUrl)
            {
                paidElsewhere[invoice.ItemId] = payUrl;
            }
        }

        return View(new MyInvoicesViewModel
        {
            Invoices = invoices,
            Payable = payable.Where(id => !paidElsewhere.ContainsKey(id)).ToHashSet(StringComparer.Ordinal),
            PaidElsewhere = paidElsewhere,
            Count = result.Count,
            Page = Math.Max(1, page),
            PageSize = _pageSize,
        });
    }

    /// <summary>
    /// Shows one of the customer's invoices.
    /// </summary>
    [HttpGet("my-invoices/{itemId}")]
    public async Task<IActionResult> Show(string itemId, [FromServices] IInvoiceDocumentBuilder builder)
    {
        var invoice = await FindVisibleAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(new InvoiceDocumentPageViewModel
        {
            Source = invoice,
            Document = await builder.BuildAsync(invoice, HttpContext.RequestAborted),
            PdfUrl = Url.Action(nameof(Pdf), new { itemId }),
            BackUrl = Url.RouteUrl(InvoicingConstants.RouteNames.MyInvoices),
            CanPay = await HttpContext.RequestServices.GetRequiredService<InvoiceLedgerAvailability>().CanPayAsync(invoice, HttpContext.RequestAborted),
        });
    }

    /// <summary>
    /// Downloads the PDF of one of the customer's invoices.
    /// </summary>
    [HttpGet("my-invoices/{itemId}/pdf")]
    public async Task<IActionResult> Pdf(string itemId, [FromServices] IInvoicePdfService pdfService)
    {
        var invoice = await FindVisibleAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(invoice, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Pays the invoices chosen, in one checkout: one invoice alone, or several owed by the same customer in one currency.
    /// </summary>
    [HttpPost("my-invoices/pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string[] itemIds)
    {
        if (await GetUserIdAsync() is not { } userId)
        {
            return Forbid();
        }

        var transactionIds = new List<string>();
        var paidElsewhere = new List<(Invoice Invoice, string PayUrl)>();
        var availability = HttpContext.RequestServices.GetRequiredService<InvoiceLedgerAvailability>();

        foreach (var itemId in (itemIds ?? []).Distinct(StringComparer.Ordinal))
        {
            var invoice = await _invoices.FindByIdAsync(itemId, HttpContext.RequestAborted);

            // Only what the customer may see and still owes is paid; anything else is left out, never refused loudly.
            if (await availability.CanPayAsync(invoice, HttpContext.RequestAborted) &&
                await _visibility.CanViewAsync(invoice, userId, HttpContext.RequestAborted))
            {
                // An order's invoice is paid through the order's own checkout, so the order hears of it.
                if (await GetOwnPayUrlAsync(invoice) is { } payUrl)
                {
                    paidElsewhere.Add((invoice, payUrl));

                    continue;
                }

                transactionIds.Add(invoice.TransactionId);
            }
        }

        // Only an invoice paid elsewhere was chosen: it is paid there.
        if (transactionIds.Count == 0 && paidElsewhere.Count == 1)
        {
            return Redirect(paidElsewhere[0].PayUrl);
        }

        foreach (var (invoice, _) in paidElsewhere)
        {
            await _notifier.InformationAsync(H["{0} is paid through its order's checkout, so it was left out of this payment. Pay it with its own Pay button.", invoice.Number]);
        }

        if (transactionIds.Count == 0)
        {
            await _notifier.WarningAsync(H["Choose at least one invoice that is still owed."]);

            return RedirectToRoute(InvoicingConstants.RouteNames.MyInvoices);
        }

        if (HttpContext.RequestServices.GetService<ICheckoutEngine>() is not { } engine)
        {
            await _notifier.WarningAsync(H["Paying online is not available on this site. Contact us to pay."]);

            return RedirectToRoute(InvoicingConstants.RouteNames.MyInvoices);
        }

        var request = new StartCheckoutRequest { ReferenceType = TransactionsConstants.ReferenceTypes.Transaction };

        if (transactionIds.Count == 1)
        {
            request.ReferenceId = transactionIds[0];
        }
        else
        {
            foreach (var transactionId in transactionIds)
            {
                request.References.Add(new CheckoutReference { ReferenceType = TransactionsConstants.ReferenceTypes.Transaction, ReferenceId = transactionId });
            }
        }

        try
        {
            var session = await engine.StartAsync(request, HttpContext.RequestAborted);

            return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId, step = session.CurrentStep });
        }
        catch (CheckoutRejectedException ex)
        {
            foreach (var error in ex.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, System.Text.Encodings.Web.HtmlEncoder.Default.Encode(error)));
            }

            return RedirectToRoute(InvoicingConstants.RouteNames.MyInvoices);
        }
    }

    // The page of its own an invoice's transaction is paid through, such as its order's checkout, or none.
    private async Task<string> GetOwnPayUrlAsync(Invoice invoice)
    {
        if (string.IsNullOrEmpty(invoice?.TransactionId) ||
            await HttpContext.RequestServices.GetRequiredService<ITransactionManager>().FindByIdAsync(invoice.TransactionId, HttpContext.RequestAborted) is not { } transaction)
        {
            return null;
        }

        foreach (var provider in HttpContext.RequestServices.GetServices<ITransactionPayUrlProvider>())
        {
            if (await provider.GetPayUrlAsync(transaction, HttpContext.RequestAborted) is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }

    private async Task<Invoice> FindVisibleAsync(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || await GetUserIdAsync() is not { } userId)
        {
            return null;
        }

        var invoice = await _invoices.FindByIdAsync(itemId, HttpContext.RequestAborted);

        return invoice is not null && await _visibility.CanViewAsync(invoice, userId, HttpContext.RequestAborted) ? invoice : null;
    }

    private async Task<string> GetUserIdAsync()
    {
        if (User.Identity?.IsAuthenticated != true || !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewOwnInvoices))
        {
            return null;
        }

        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
