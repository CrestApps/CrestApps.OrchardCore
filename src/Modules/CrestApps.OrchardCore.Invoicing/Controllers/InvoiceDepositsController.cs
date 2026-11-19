using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// Asks for a deposit before the work on a draft invoice, and deducts a paid deposit from it.
/// </summary>
[Admin("invoices/deposits/{action}/{itemId}", "InvoiceDeposits{action}")]
public sealed class InvoiceDepositsController : Controller
{
    private readonly IInvoicePrepaymentService _prepayments;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public InvoiceDepositsController(
        IInvoicePrepaymentService prepayments,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<InvoiceDepositsController> htmlLocalizer)
    {
        _prepayments = prepayments;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Makes a draft deposit invoice for a percent or an amount of the draft invoice, and opens it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ask(string itemId, decimal? percent, decimal? amount)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var result = await _prepayments.RequestDepositAsync(itemId, percent, amount, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await WarnAsync(result.Errors);

            return RedirectToAction("Detail", "InvoicesAdmin", new { itemId });
        }

        await _notifier.SuccessAsync(H["The deposit invoice was drafted. Issue and send it; once it is paid, deduct it from this invoice."]);

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId = result.Invoice.ItemId });
    }

    /// <summary>
    /// Deducts what was paid on a deposit invoice from the draft invoice.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Deduct(string itemId, string prepaymentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var result = await _prepayments.DeductAsync(itemId, prepaymentId, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The deposit paid was deducted from this invoice."]);
        }
        else
        {
            await WarnAsync(result.Errors);
        }

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId });
    }

    private async Task WarnAsync(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
        }
    }
}
