using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Recurring;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Services;
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

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The recurring plans of the admin: the list, the editor, the plan's page with its upcoming and past runs, and its
/// actions (start, pause, resume, cancel, run now).
/// </summary>
[Admin("recurring-invoices/{action}/{itemId?}", "RecurringInvoices{action}")]
[Feature(InvoicingConstants.Features.Recurring)]
public sealed class RecurringPlansAdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";

    private readonly IRecurringPlanManager _plans;
    private readonly IRecurringPlanService _service;
    private readonly IDocumentCalculator _calculator;
    private readonly IPaymentTermManager _paymentTerms;
    private readonly IPartyDirectory _parties;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public RecurringPlansAdminController(
        IRecurringPlanManager plans,
        IRecurringPlanService service,
        IDocumentCalculator calculator,
        IPaymentTermManager paymentTerms,
        IPartyDirectory parties,
        ISiteService siteService,
        ILocalClock localClock,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<RecurringPlansAdminController> htmlLocalizer,
        IStringLocalizer<RecurringPlansAdminController> stringLocalizer)
    {
        _plans = plans;
        _service = service;
        _calculator = calculator;
        _paymentTerms = paymentTerms;
        _parties = parties;
        _siteService = siteService;
        _localClock = localClock;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the plans, newest first, with a search and a status filter.
    /// </summary>
    [Admin("recurring-invoices", "RecurringInvoicesIndex")]
    public async Task<IActionResult> Index(
        RecurringPlansAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _plans.PageAsync(pager.Page, pager.PageSize, new RecurringPlanQuery { Search = options.Search, Status = options.Status });

        var routeData = new RouteData();
        AddFilterRouteValues(options, routeData.Values);

        options.Statuses =
        [
            new(S["Any status"], string.Empty, options.Status is null),
            new(S["Draft"], nameof(RecurringPlanStatus.Draft), options.Status == RecurringPlanStatus.Draft),
            new(S["Active"], nameof(RecurringPlanStatus.Active), options.Status == RecurringPlanStatus.Active),
            new(S["Paused"], nameof(RecurringPlanStatus.Paused), options.Status == RecurringPlanStatus.Paused),
            new(S["Completed"], nameof(RecurringPlanStatus.Completed), options.Status == RecurringPlanStatus.Completed),
            new(S["Canceled"], nameof(RecurringPlanStatus.Canceled), options.Status == RecurringPlanStatus.Canceled),
        ];

        var filters = await shapeFactory.CreateAdminListFiltersAsync(
            "RecurringPlans",
            "recurring-plans-filter",
            new AdminListFilter(_optionsStatus, options.Statuses, S["Filter by status"], S["Status"]));

        return View(new RecurringPlansAdminIndexViewModel
        {
            Options = options,
            CanCreate = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices),
            List = new AdminListContext<RecurringPlan>
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
    [Admin("recurring-invoices", "RecurringInvoicesIndex")]
    public async Task<IActionResult> IndexFilterPost(RecurringPlansAdminIndexOptions options, PagerParameters pagerParameters)
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
    /// Opens a new plan, starting today, monthly.
    /// </summary>
    public async Task<IActionResult> Create(string billTo)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        var settings = await GetSettingsAsync();
        var plan = await _plans.NewAsync();

        plan.Currency = settings.DefaultCurrency;
        plan.PricesIncludeTax = settings.PricesIncludeTax;
        plan.StartDate = await GetTodayAsync();
        plan.Rule = "FREQ=MONTHLY";

        if (PartyRef.TryParse(billTo, out var party))
        {
            plan.BillTo.Party = party;
        }

        var model = Build(plan);
        model.ItemId = null;

        return View("Edit", await PrepareAsync(model, plan));
    }

    /// <summary>
    /// Saves a new plan.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(RecurringPlanEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        return await SaveAsync(await _plans.NewAsync(), model, submit, isNew: true);
    }

    /// <summary>
    /// Opens a plan in the editor; a completed or canceled plan opens its page.
    /// </summary>
    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        var plan = await FindAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        if (plan.Status is RecurringPlanStatus.Completed or RecurringPlanStatus.Canceled)
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        return View(await PrepareAsync(Build(plan), plan));
    }

    /// <summary>
    /// Saves a plan. A change applies to the runs after it.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, RecurringPlanEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        var plan = await FindAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        if (plan.Status is RecurringPlanStatus.Completed or RecurringPlanStatus.Canceled)
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        return await SaveAsync(plan, model, submit, isNew: false);
    }

    /// <summary>
    /// Shows a plan with its next runs and the runs it made.
    /// </summary>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var plan = await FindAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        return View(new RecurringPlanDetailViewModel
        {
            Plan = plan,
            Upcoming = plan.Status is RecurringPlanStatus.Draft or RecurringPlanStatus.Active ? _service.GetUpcoming(plan, 6) : [],
            CanManage = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices),
        });
    }

    /// <summary>
    /// Starts a draft plan.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Activate(string itemId)
        => await CommandAsync(itemId, () => _service.ActivateAsync(itemId, HttpContext.RequestAborted), H["The plan was started."]);

    /// <summary>
    /// Pauses an active plan.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Pause(string itemId)
        => await CommandAsync(itemId, () => _service.PauseAsync(itemId, HttpContext.RequestAborted), H["The plan was paused. The runs due while it is paused are skipped."]);

    /// <summary>
    /// Resumes a paused plan from today.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Resume(string itemId)
        => await CommandAsync(itemId, async () => await _service.ResumeAsync(itemId, await GetTodayAsync(), HttpContext.RequestAborted), H["The plan was resumed."]);

    /// <summary>
    /// Cancels a plan for good.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId)
        => await CommandAsync(itemId, () => _service.CancelAsync(itemId, HttpContext.RequestAborted), H["The plan was canceled."]);

    /// <summary>
    /// Makes the invoices of the runs due today, now.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RunNow(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        var result = await _service.RunAsync(itemId, await GetTodayAsync(), HttpContext.RequestAborted);

        return await CompleteAsync(result, itemId, result.Invoices.Count == 0 ? H["No run was due."] : H.Plural(result.Invoices.Count, "1 invoice was made.", "{0} invoices were made."));
    }

    /// <summary>
    /// Deletes a draft plan.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        var plan = await FindAsync(itemId);

        if (plan is null)
        {
            return NotFound();
        }

        if (plan.Status != RecurringPlanStatus.Draft)
        {
            await _notifier.WarningAsync(H["Only a draft plan can be deleted. Cancel it instead."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        await _plans.DeleteAsync(plan);
        await _notifier.SuccessAsync(H["The plan was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> CommandAsync(string itemId, Func<Task<RecurringPlanResult>> command, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageRecurringInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await command(), itemId, success);
    }

    private async Task<IActionResult> CompleteAsync(RecurringPlanResult result, string itemId, LocalizedHtmlString success)
    {
        if (result.Plan is null && result.Errors.Count == 0)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
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

    private async Task<IActionResult> SaveAsync(RecurringPlan plan, RecurringPlanEditViewModel model, string submit, bool isNew)
    {
        Apply(model, plan);

        if (!string.IsNullOrEmpty(plan.Currency) && plan.Currency.Length != 3)
        {
            ModelState.AddModelError(nameof(model.Currency), S["The currency must be a three-letter code, such as USD."]);
        }

        if (!string.IsNullOrEmpty(model.BillTo) && plan.BillTo.Party.IsEmpty)
        {
            ModelState.AddModelError(nameof(model.BillTo), S["Pick who the plan bills from the list."]);
        }

        await InvoiceCalculation.ApplyAsync(_calculator, plan, await GetSettingsAsync(), issueDate: null, HttpContext.RequestAborted);

        foreach (var error in (await _plans.ValidateAsync(plan)).Errors)
        {
            ModelState.AddModelError(error.MemberNames?.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            var rebuilt = Build(plan);
            rebuilt.Frequency = model.Frequency;
            rebuilt.Interval = model.Interval;
            rebuilt.CustomRule = model.CustomRule;

            if (isNew)
            {
                rebuilt.ItemId = null;
            }

            return View("Edit", await PrepareAsync(rebuilt, plan));
        }

        if (isNew)
        {
            await _plans.CreateAsync(plan);
        }
        else
        {
            await _plans.UpdateAsync(plan);
        }

        await _notifier.SuccessAsync(H["The plan was saved."]);

        return string.Equals(submit, "update", StringComparison.Ordinal)
            ? RedirectToAction(nameof(Edit), new { itemId = plan.ItemId })
            : RedirectToAction(nameof(Detail), new { itemId = plan.ItemId });
    }

    private static RecurringPlanEditViewModel Build(RecurringPlan plan)
    {
        var (frequency, interval, customRule) = RecurringSchedule.Read(plan.Rule);
        var model = new RecurringPlanEditViewModel
        {
            ItemId = plan.ItemId,
            Name = plan.Name,
            Frequency = frequency,
            Interval = interval,
            CustomRule = customRule,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            MaxRuns = plan.MaxRuns,
            Mode = plan.Mode,
            PriceIncreasePercent = plan.PriceIncreasePercent,
            PriceIncreaseEveryRuns = plan.PriceIncreaseEveryRuns,
            BillTo = plan.BillTo?.Party.IsEmpty == false ? plan.BillTo.Party.ToString() : null,
            ContactName = plan.BillTo?.Contact?.Name,
            ContactEmail = plan.BillTo?.Contact?.Email,
            Currency = plan.Currency,
            PaymentTermId = plan.PaymentTermId,
            PurchaseOrderNumber = plan.PurchaseOrderNumber,
            Reference = plan.Reference,
            PricesIncludeTax = plan.PricesIncludeTax,
            Notes = plan.Notes,
            Terms = plan.Terms,
            Footer = plan.Footer,
            InternalNote = plan.InternalNote,
            Totals = plan.Totals,
            Taxes = plan.Taxes ?? [],
            Lines = InvoiceDraftEditor.BuildLines(plan.Lines),
        };

        model.Lines.Add(new InvoiceLineEditViewModel { Quantity = 1m });

        return model;
    }

    private static void Apply(RecurringPlanEditViewModel model, RecurringPlan plan)
    {
        plan.Name = Clean(model.Name);
        plan.Rule = RecurringSchedule.BuildRule(model.Frequency, Math.Clamp(model.Interval, 1, 99), model.CustomRule);
        plan.StartDate = model.StartDate;
        plan.EndDate = model.EndDate;
        plan.MaxRuns = model.MaxRuns is > 0 ? model.MaxRuns : null;
        plan.Mode = model.Mode;
        plan.PriceIncreasePercent = model.PriceIncreasePercent is > 0m ? model.PriceIncreasePercent : null;
        plan.PriceIncreaseEveryRuns = model.PriceIncreaseEveryRuns is > 0 ? model.PriceIncreaseEveryRuns : null;
        plan.BillTo ??= new DocumentParty();
        plan.BillTo.Party = PartyRef.TryParse(model.BillTo?.Trim(), out var party) ? party : default;
        plan.BillTo.Contact = (plan.BillTo.Contact ?? new ContactInfo()) with { Name = Clean(model.ContactName), Email = Clean(model.ContactEmail) };
        plan.Currency = Clean(model.Currency)?.ToUpperInvariant();
        plan.PaymentTermId = Clean(model.PaymentTermId);
        plan.PurchaseOrderNumber = Clean(model.PurchaseOrderNumber);
        plan.Reference = Clean(model.Reference);
        plan.PricesIncludeTax = model.PricesIncludeTax;
        plan.Notes = Clean(model.Notes);
        plan.Terms = Clean(model.Terms);
        plan.Footer = Clean(model.Footer);
        plan.InternalNote = Clean(model.InternalNote);

        InvoiceDraftEditor.ApplyLines(model.Lines, plan, keepText: false);

        // A change to the schedule of a running plan moves its next run along the new rule.
        if (plan.Status == RecurringPlanStatus.Active && plan.NextRunDate is { } next)
        {
            plan.NextRunDate = DefaultRecurringPlanService.NextOnOrAfter(plan, next);
        }
    }

    private async Task<RecurringPlanEditViewModel> PrepareAsync(RecurringPlanEditViewModel model, RecurringPlan plan)
    {
        model.PaymentTerms = [.. (await _paymentTerms.GetEnabledAsync()).Select(term => new SelectListItem(term.Name, term.ItemId, string.Equals(term.ItemId, model.PaymentTermId, StringComparison.Ordinal)))];
        model.LineKinds =
        [
            new(S["Item"], nameof(DocumentLineKind.Item)),
            new(S["Service"], nameof(DocumentLineKind.Service)),
            new(S["Fee"], nameof(DocumentLineKind.Fee)),
            new(S["Text"], nameof(DocumentLineKind.Text)),
            new(S["Section heading"], nameof(DocumentLineKind.Section)),
        ];
        model.PartySearchEndpoint = Url.RouteUrl(InvoicePartySearchEndpoints.RouteName);
        model.Totals ??= plan.Totals;

        if (PartyRef.TryParse(model.BillTo, out var party))
        {
            model.BillToName = await _parties.GetNameAsync(party) ?? model.BillTo;
        }

        return model;
    }

    private async Task<RecurringPlan> FindAsync(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : await _plans.FindByIdAsync(itemId);

    private async Task<InvoicingSettings> GetSettingsAsync()
        => await _siteService.GetSettingsAsync<InvoicingSettings>();

    private async Task<DateOnly> GetTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddFilterRouteValues(RecurringPlansAdminIndexOptions options, RouteValueDictionary values)
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
}
