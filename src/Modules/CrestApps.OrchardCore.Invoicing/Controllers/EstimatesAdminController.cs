using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;
using YesSql;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The estimates of the admin: the list, the draft editor, the estimate's page and its actions (send, record the
/// customer's answer, revise, cancel, invoice).
/// </summary>
[Admin("estimates/{action}/{itemId?}", "Estimates{action}")]
[Feature(InvoicingConstants.Features.Estimates)]
public sealed class EstimatesAdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";
    private const string _submitSend = "send";
    private const string _submitUpdate = "update";

    private readonly IEstimateManager _estimates;
    private readonly IEstimateCommandService _commands;
    private readonly IInvoiceManager _invoices;
    private readonly IDocumentCalculator _calculator;
    private readonly IPartyDirectory _parties;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;
    private readonly IDisplayManager<EstimateEditViewModel> _editorManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public EstimatesAdminController(
        IEstimateManager estimates,
        IEstimateCommandService commands,
        IInvoiceManager invoices,
        IDocumentCalculator calculator,
        IPartyDirectory parties,
        ISiteService siteService,
        ILocalClock localClock,
        IAuthorizationService authorizationService,
        ISession session,
        IDisplayManager<EstimateEditViewModel> editorManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IHtmlLocalizer<EstimatesAdminController> htmlLocalizer,
        IStringLocalizer<EstimatesAdminController> stringLocalizer)
    {
        _estimates = estimates;
        _commands = commands;
        _invoices = invoices;
        _calculator = calculator;
        _parties = parties;
        _siteService = siteService;
        _localClock = localClock;
        _authorizationService = authorizationService;
        _session = session;
        _editorManager = editorManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the estimates, newest first, with a search and a status filter.
    /// </summary>
    [Admin("estimates", "EstimatesIndex")]
    public async Task<IActionResult> Index(
        EstimatesAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _estimates.PageAsync(pager.Page, pager.PageSize, new EstimateQuery { Search = options.Search, Status = options.Status });

        var routeData = new RouteData();
        AddFilterRouteValues(options, routeData.Values);

        options.Statuses = BuildStatusFilterItems(options.Status);

        var filters = await shapeFactory.CreateAdminListFiltersAsync(
            "Estimates",
            "estimates-filter",
            new AdminListFilter(_optionsStatus, options.Statuses, S["Filter by status"], S["Status"]));

        return View(new EstimatesAdminIndexViewModel
        {
            Options = options,
            CanCreate = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates),
            List = new AdminListContext<Estimate>
            {
                Source = result.Entries,
                ToolbarActions = filters,
                Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Keeps the search and filter in the address when the list's form is submitted.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("estimates", "EstimatesIndex")]
    public async Task<IActionResult> IndexFilterPost(EstimatesAdminIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();
        AddFilterRouteValues(options, routeValues);

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Opens a new draft, with the defaults of the invoicing settings.
    /// </summary>
    public async Task<IActionResult> Create(string billTo)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var settings = await GetSettingsAsync();
        var estimate = await _estimates.NewAsync();

        estimate.Currency = settings.DefaultCurrency;
        estimate.PricesIncludeTax = settings.PricesIncludeTax;

        if (PartyRef.TryParse(billTo, out var party))
        {
            estimate.BillTo.Party = party;
        }

        var model = new EstimateEditViewModel();
        EstimateDraftEditor.Build(estimate, model);
        model.ItemId = null;

        return View("Edit", await PrepareAsync(model, estimate));
    }

    /// <summary>
    /// Saves a new draft, and sends it when asked.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(EstimateEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        return await SaveAsync(await _estimates.NewAsync(), model, submit, isNew: true);
    }

    /// <summary>
    /// Opens a draft in the editor. A sent estimate opens its page: it is changed by revising it.
    /// </summary>
    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var estimate = await FindAsync(itemId);

        if (estimate is null)
        {
            return NotFound();
        }

        if (estimate.Status != EstimateStatus.Draft)
        {
            await _notifier.WarningAsync(H["A sent estimate can't be changed. Revise it instead."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var model = new EstimateEditViewModel();
        EstimateDraftEditor.Build(estimate, model);

        return View(await PrepareAsync(model, estimate));
    }

    /// <summary>
    /// Saves a draft, and sends it when asked.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, EstimateEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var estimate = await FindAsync(itemId);

        if (estimate is null)
        {
            return NotFound();
        }

        if (estimate.Status != EstimateStatus.Draft)
        {
            await _notifier.WarningAsync(H["A sent estimate can't be changed. Revise it instead."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        return await SaveAsync(estimate, model, submit, isNew: false);
    }

    /// <summary>
    /// Shows an estimate as the customer sees it, with its history and actions.
    /// </summary>
    public async Task<IActionResult> Detail(string itemId, [FromServices] IInvoiceDocumentBuilder builder, [FromServices] EstimateShareLinkService links, [FromServices] EstimateSender sender)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var estimate = await FindAsync(itemId);

        if (estimate is null)
        {
            return NotFound();
        }

        var invoices = new List<Invoice>();

        foreach (var invoiceId in estimate.InvoiceIds)
        {
            if (await _invoices.FindByIdAsync(invoiceId) is { } invoice)
            {
                invoices.Add(invoice);
            }
        }

        return View(new EstimateDetailViewModel
        {
            Estimate = estimate,
            Document = await builder.BuildAsync(estimate, HttpContext.RequestAborted),
            ShareUrl = estimate.Status is EstimateStatus.Sent or EstimateStatus.Accepted or EstimateStatus.Invoiced ? await links.CreateUrlAsync(estimate) : null,
            CanManage = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates),
            CanEmail = sender.CanSend,
            CanConvertDeposit = (estimate.DepositAmount is > 0m || estimate.DepositPercent is > 0m) && !invoices.Any(invoice => invoice.Kind == InvoiceKind.Prepayment && invoice.Status != InvoiceStatus.Void),
            Invoices = invoices,
            InvoicedPercents = await _commands.GetInvoicedPercentsAsync(estimate.ItemId, HttpContext.RequestAborted),
        });
    }

    /// <summary>
    /// Downloads the estimate's PDF.
    /// </summary>
    public async Task<IActionResult> Pdf(string itemId, [FromServices] IInvoicePdfService pdfService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var estimate = await FindAsync(itemId);

        if (estimate is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(estimate, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Sends the estimate: e-mails it when e-mail is set up, and otherwise only numbers it so its link can be shared.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Send(string itemId, [FromServices] EstimateSender sender)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        if (sender.CanSend)
        {
            var mailed = await sender.SendAsync(itemId, to: null, HttpContext.RequestAborted);

            return await CompleteAsync(mailed, itemId, mailed.Estimate is { } estimate ? H["Estimate {0} was e-mailed to the customer.", estimate.Number] : null);
        }

        var sent = await _commands.SendAsync(itemId, HttpContext.RequestAborted);

        return await CompleteAsync(sent, itemId, sent.Estimate is { } numbered ? H["Estimate {0} is ready to share. E-mail isn't set up, so copy its link below.", numbered.Number] : null);
    }

    /// <summary>
    /// Records that the customer accepted, when they said so outside the site.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Accept(string itemId, string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var result = await _commands.AcceptAsync(itemId, new EstimateAcceptanceRequest
        {
            Name = name,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Today = await GetTodayAsync(),
        }, HttpContext.RequestAborted);

        return await CompleteAsync(result, itemId, H["The estimate was marked accepted."]);
    }

    /// <summary>
    /// Records that the customer declined.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Decline(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.DeclineAsync(itemId, reason, HttpContext.RequestAborted), itemId, H["The estimate was marked declined."]);
    }

    /// <summary>
    /// Withdraws the estimate.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.CancelAsync(itemId, reason, HttpContext.RequestAborted), itemId, H["The estimate was canceled."]);
    }

    /// <summary>
    /// Starts a revision and opens it in the editor.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Revise(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var result = await _commands.ReviseAsync(itemId, HttpContext.RequestAborted);

        if (!result.Succeeded || result.Revision is null)
        {
            return await CompleteAsync(result, itemId, success: null);
        }

        await _notifier.SuccessAsync(H["Revision {0} was started. Change it and send it.", result.Revision.Revision]);

        return RedirectToAction(nameof(Edit), new { itemId = result.Revision.ItemId });
    }

    /// <summary>
    /// Turns the estimate into a draft invoice, of the whole of it or of its deposit, and opens it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Convert(string itemId, EstimateConversion conversion)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates) ||
            !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var result = await _commands.ConvertAsync(itemId, conversion, HttpContext.RequestAborted);

        if (!result.Succeeded || result.Invoice is null)
        {
            return await CompleteAsync(result, itemId, success: null);
        }

        await _notifier.SuccessAsync(result.AlreadyDone ? H["This estimate was invoiced already: here is its invoice."] : H["A draft invoice was made from the estimate. Check it and issue it."]);

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId = result.Invoice.ItemId });
    }

    /// <summary>
    /// Invoices a percent of each line chosen (progress invoicing) on a draft invoice, and opens it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Progress(string itemId, Dictionary<string, decimal> percents)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates) ||
            !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var result = await _commands.InvoiceProgressAsync(itemId, percents ?? [], HttpContext.RequestAborted);

        if (!result.Succeeded || result.Invoice is null)
        {
            return await CompleteAsync(result, itemId, success: null);
        }

        await _notifier.SuccessAsync(H["A draft invoice was made for the part chosen. Check it and issue it."]);

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId = result.Invoice.ItemId });
    }

    /// <summary>
    /// Deletes a draft.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates))
        {
            return Forbid();
        }

        var estimate = await FindAsync(itemId);

        if (estimate is null)
        {
            return NotFound();
        }

        if (estimate.Status != EstimateStatus.Draft)
        {
            await _notifier.WarningAsync(H["A sent estimate can't be deleted. Cancel it instead."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        await _estimates.DeleteAsync(estimate);
        await _notifier.SuccessAsync(H["The draft was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(Estimate estimate, EstimateEditViewModel model, string submit, bool isNew)
    {
        model.Editor = await _editorManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew);

        EstimateDraftEditor.Apply(model, estimate);

        if (!string.IsNullOrEmpty(estimate.Currency) && estimate.Currency.Length != 3)
        {
            ModelState.AddModelError(nameof(model.Currency), S["The currency must be a three-letter code, such as USD."]);
        }

        if (!string.IsNullOrEmpty(model.BillTo) && estimate.BillTo.Party.IsEmpty)
        {
            ModelState.AddModelError(nameof(model.BillTo), S["Pick who the estimate is for from the list."]);
        }

        await InvoiceCalculation.ApplyAsync(_calculator, estimate, await GetSettingsAsync(), issueDate: null, HttpContext.RequestAborted);

        foreach (var error in (await _estimates.ValidateAsync(estimate)).Errors)
        {
            ModelState.AddModelError(error.MemberNames?.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            EstimateDraftEditor.Build(estimate, model);

            if (isNew)
            {
                model.ItemId = null;
            }

            return View("Edit", await PrepareAsync(model, estimate));
        }

        if (isNew)
        {
            await _estimates.CreateAsync(estimate);
        }
        else
        {
            await _estimates.UpdateAsync(estimate);
        }

        if (string.Equals(submit, _submitUpdate, StringComparison.Ordinal))
        {
            await _notifier.SuccessAsync(H["The draft was saved."]);

            return RedirectToAction(nameof(Edit), new { itemId = estimate.ItemId });
        }

        if (!string.Equals(submit, _submitSend, StringComparison.Ordinal))
        {
            await _notifier.SuccessAsync(H["The draft was saved."]);

            return RedirectToAction(nameof(Detail), new { itemId = estimate.ItemId });
        }

        // Sending reads the draft under its lock, so the draft is written first.
        await _session.SaveChangesAsync(HttpContext.RequestAborted);

        var sent = await _commands.SendAsync(estimate.ItemId, HttpContext.RequestAborted);

        return await CompleteAsync(sent, estimate.ItemId, sent.Estimate is { } numbered ? H["Estimate {0} was sent. Share its link or e-mail it from its page.", numbered.Number] : null);
    }

    private async Task<EstimateEditViewModel> PrepareAsync(EstimateEditViewModel model, Estimate estimate)
    {
        model.LineKinds =
        [
            new(S["Item"], nameof(DocumentLineKind.Item)),
            new(S["Service"], nameof(DocumentLineKind.Service)),
            new(S["Time"], nameof(DocumentLineKind.Time)),
            new(S["Expense"], nameof(DocumentLineKind.Expense)),
            new(S["Fee"], nameof(DocumentLineKind.Fee)),
            new(S["Text"], nameof(DocumentLineKind.Text)),
            new(S["Section heading"], nameof(DocumentLineKind.Section)),
        ];
        model.PartySearchEndpoint = Url.RouteUrl(InvoicePartySearchEndpoints.RouteName);
        model.CanSend = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageEstimates);
        model.Totals ??= estimate.Totals;

        if (PartyRef.TryParse(model.BillTo, out var party))
        {
            model.BillToName = await _parties.GetNameAsync(party) ?? model.BillTo;
        }

        model.Editor = await _editorManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: string.IsNullOrEmpty(model.ItemId));

        return model;
    }

    private async Task<IActionResult> CompleteAsync(EstimateCommandResult result, string itemId, LocalizedHtmlString success)
    {
        if (result.Estimate is null && result.Errors.Count == 0)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            if (success is not null)
            {
                await _notifier.SuccessAsync(success);
            }
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
            }
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<Estimate> FindAsync(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : await _estimates.FindByIdAsync(itemId);

    private async Task<InvoicingSettings> GetSettingsAsync()
        => await _siteService.GetSettingsAsync<InvoicingSettings>();

    private async Task<DateOnly> GetTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private static void AddFilterRouteValues(EstimatesAdminIndexOptions options, RouteValueDictionary values)
    {
        if (!string.IsNullOrEmpty(options.Search))
        {
            values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status.HasValue)
        {
            values.TryAdd(_optionsStatus, options.Status.Value);
        }
    }

    private List<SelectListItem> BuildStatusFilterItems(EstimateStatus? selected)
        =>
        [
            new(S["Any status"], string.Empty, selected is null),
            new(S["Draft"], nameof(EstimateStatus.Draft), selected == EstimateStatus.Draft),
            new(S["Awaiting answer"], nameof(EstimateStatus.Sent), selected == EstimateStatus.Sent),
            new(S["Accepted"], nameof(EstimateStatus.Accepted), selected == EstimateStatus.Accepted),
            new(S["Invoiced"], nameof(EstimateStatus.Invoiced), selected == EstimateStatus.Invoiced),
            new(S["Declined"], nameof(EstimateStatus.Declined), selected == EstimateStatus.Declined),
            new(S["Expired"], nameof(EstimateStatus.Expired), selected == EstimateStatus.Expired),
            new(S["Canceled"], nameof(EstimateStatus.Canceled), selected == EstimateStatus.Canceled),
        ];
}
