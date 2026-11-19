using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The public page of an estimate, opened by its signed link: the customer reads it, downloads its PDF, and accepts or
/// declines it while it is valid. No sign-in is needed.
/// </summary>
[Feature(InvoicingConstants.Features.Estimates)]
public sealed class EstimateController : Controller
{
    private readonly EstimateShareLinkService _links;
    private readonly IEstimateStore _estimates;
    private readonly IEstimateCommandService _commands;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public EstimateController(
        EstimateShareLinkService links,
        IEstimateStore estimates,
        IEstimateCommandService commands,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<EstimateController> htmlLocalizer)
    {
        _links = links;
        _estimates = estimates;
        _commands = commands;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the estimate a link names.
    /// </summary>
    public async Task<IActionResult> Show(string token, [FromServices] IInvoiceDocumentBuilder builder)
    {
        var estimate = await FindAsync(token);

        if (estimate is null)
        {
            return View("Invalid");
        }

        var today = await GetTodayAsync();
        var expired = estimate.Status == EstimateStatus.Expired || estimate.Status == EstimateStatus.Sent && estimate.ExpiryDate is { } expiry && expiry < today;

        return View(new EstimatePublicViewModel
        {
            Estimate = estimate,
            Document = await builder.BuildAsync(estimate, HttpContext.RequestAborted),
            Token = token,
            PdfUrl = Url.RouteUrl(InvoicingConstants.RouteNames.EstimateView + "Pdf", new { token }),
            CanAnswer = estimate.Status == EstimateStatus.Sent && !expired,
            IsExpired = expired,
        });
    }

    /// <summary>
    /// Downloads the PDF of the estimate a link names.
    /// </summary>
    public async Task<IActionResult> Pdf(string token, [FromServices] IInvoicePdfService pdfService)
    {
        var estimate = await FindAsync(token);

        if (estimate is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(estimate, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Accepts the estimate, with the name the customer typed.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(string token, string name)
    {
        var estimate = await FindAsync(token);

        if (estimate is null)
        {
            return NotFound();
        }

        var result = await _commands.AcceptAsync(estimate.ItemId, new EstimateAcceptanceRequest
        {
            Name = name,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            Today = await GetTodayAsync(),
        }, HttpContext.RequestAborted);

        // An estimate that stands for something paid through its own checkout, such as an order's quote, is paid there.
        if (result.Succeeded && result.Estimate is { } accepted)
        {
            foreach (var provider in HttpContext.RequestServices.GetServices<IEstimatePayLinkProvider>())
            {
                if (await provider.GetPayUrlAsync(accepted, HttpContext.RequestAborted) is { Length: > 0 } payUrl)
                {
                    return Redirect(payUrl);
                }
            }
        }

        await NotifyAsync(result, H["Thank you: you accepted the estimate. We'll be in touch."]);

        return RedirectToRoute(InvoicingConstants.RouteNames.EstimateView, new { token });
    }

    /// <summary>
    /// Declines the estimate, with the reason the customer gave, if any.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(string token, string reason)
    {
        var estimate = await FindAsync(token);

        if (estimate is null)
        {
            return NotFound();
        }

        var result = await _commands.DeclineAsync(estimate.ItemId, reason, HttpContext.RequestAborted);

        await NotifyAsync(result, H["You declined the estimate. Thank you for letting us know."]);

        return RedirectToRoute(InvoicingConstants.RouteNames.EstimateView, new { token });
    }

    private async Task NotifyAsync(EstimateCommandResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in result.Errors)
        {
            await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
        }
    }

    private async Task<Estimate> FindAsync(string token)
    {
        var id = _links.GetEstimateId(token);

        // A draft has no public page.
        return string.IsNullOrEmpty(id) || await _estimates.FindByIdAsync(id, HttpContext.RequestAborted) is not { } estimate || estimate.Status == EstimateStatus.Draft
            ? null
            : estimate;
    }

    private async Task<DateOnly> GetTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
}
