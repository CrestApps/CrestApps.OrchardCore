using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Collections;

/// <summary>
/// Commerce > Statements: a customer's open-item or activity statement, as a page, a PDF and an e-mail.
/// </summary>
[Admin("statements/{action}", "InvoiceStatements{action}")]
[Feature(InvoicingConstants.Features.Collections)]
public sealed class StatementsAdminController : Controller
{
    private readonly IInvoiceStatementService _statements;
    private readonly IPartyDirectory _parties;
    private readonly ILocalClock _localClock;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public StatementsAdminController(
        IInvoiceStatementService statements,
        IPartyDirectory parties,
        ILocalClock localClock,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<StatementsAdminController> htmlLocalizer)
    {
        _statements = statements;
        _parties = parties;
        _localClock = localClock;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the statement of the customer chosen, or the form to choose one.
    /// </summary>
    [Admin("statements", "InvoiceStatementsIndex")]
    public async Task<IActionResult> Index(InvoiceStatementViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        await PrepareAsync(model);

        if (model.Request is { } request)
        {
            model.Statement = await _statements.BuildAsync(request, HttpContext.RequestAborted);
            model.Document = await _statements.BuildDocumentAsync(model.Statement, HttpContext.RequestAborted);
        }

        return View(model);
    }

    /// <summary>
    /// Downloads the statement as a PDF.
    /// </summary>
    public async Task<IActionResult> Pdf(InvoiceStatementViewModel model, [FromServices] IEnumerable<IBusinessDocumentPdfRenderer> renderers)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        await PrepareAsync(model);

        if (model.Request is not { } request || renderers.FirstOrDefault() is not { } renderer)
        {
            return NotFound();
        }

        var statement = await _statements.BuildAsync(request, HttpContext.RequestAborted);
        var pdf = await renderer.RenderAsync(await _statements.BuildDocumentAsync(statement, HttpContext.RequestAborted), HttpContext.RequestAborted);

        return File(pdf, "application/pdf", $"statement-{request.To:yyyy-MM-dd}.pdf");
    }

    /// <summary>
    /// E-mails the statement to the customer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Send(InvoiceStatementViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        await PrepareAsync(model);

        if (model.Request is { } request)
        {
            var error = await _statements.SendAsync(await _statements.BuildAsync(request, HttpContext.RequestAborted), model.SendTo, HttpContext.RequestAborted);

            if (error is null)
            {
                await _notifier.SuccessAsync(H["The statement was e-mailed."]);
            }
            else
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, "{0}", false, error));
            }
        }

        // The dates go back as the date fields send them, whatever the culture.
        return RedirectToAction(nameof(Index), new
        {
            model.Party,
            model.Kind,
            From = model.From?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            To = model.To?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            model.Currency,
        });
    }

    private async Task PrepareAsync(InvoiceStatementViewModel model)
    {
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

        model.To ??= today;
        model.From ??= model.Kind == InvoiceStatementKind.Activity ? new DateOnly(model.To.Value.Year, model.To.Value.Month, 1) : null;
        model.PartySearchEndpoint = Url.RouteUrl(InvoicePartySearchEndpoints.RouteName);
        model.CanPrint = HttpContext.RequestServices.GetService(typeof(IBusinessDocumentPdfRenderer)) is not null;

        if (PartyRef.TryParse(model.Party, out var party))
        {
            model.PartyName = await _parties.GetNameAsync(party) ?? model.Party;
            model.Request = new InvoiceStatementRequest
            {
                Party = party,
                Kind = model.Kind,
                From = model.Kind == InvoiceStatementKind.Activity ? model.From : null,
                To = model.To.Value,
                Currency = string.IsNullOrWhiteSpace(model.Currency) ? null : model.Currency.Trim(),
            };
        }
    }
}
