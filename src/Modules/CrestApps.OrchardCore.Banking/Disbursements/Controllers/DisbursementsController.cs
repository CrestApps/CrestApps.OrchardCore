using System.Globalization;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Banking.Disbursements.ViewModels;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Banking.Disbursements.Controllers;

/// <summary>
/// Banking › Disbursements: the money the business sends out, disbursements written by hand, batching and cancelling.
/// </summary>
[Admin]
[Feature(DisbursementsConstants.Features.Disbursements)]
public sealed class DisbursementsController : Controller
{
    private readonly IDisbursementManager _manager;
    private readonly IDisbursementStore _store;
    private readonly IDisbursementService _disbursementService;
    private readonly ISourceAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<Disbursement> _displayManager;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DisbursementsController(
        IDisbursementManager manager,
        IDisbursementStore store,
        IDisbursementService disbursementService,
        ISourceAccessService access,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<Disbursement> displayManager,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<DisbursementsController> htmlLocalizer,
        IStringLocalizer<DisbursementsController> stringLocalizer)
    {
        _manager = manager;
        _store = store;
        _disbursementService = disbursementService;
        _access = access;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _localClock = localClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("banking/disbursements", "DisbursementsIndex")]
    public async Task<IActionResult> Index(
        DisbursementsIndexViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ViewDisbursements))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, disbursements) = await _store.ListPageAsync(
            new DisbursementQuery
            {
                Status = Enum.TryParse<DisbursementStatus>(filter.Status, out var status) ? status : null,
                Method = string.IsNullOrEmpty(filter.Method) ? null : filter.Method,
                BatchId = string.IsNullOrEmpty(filter.BatchId) ? null : filter.BatchId,
                Search = filter.Search,
                Access = await _access.GetFilterAsync(User, SourceAccessLevel.View, HttpContext.RequestAborted),
            },
            pager.GetStartIndex(),
            pager.PageSize,
            HttpContext.RequestAborted);

        var routeData = new RouteData();

        foreach (var (key, value) in new[] { ("Search", filter.Search), ("Status", filter.Status), ("Method", filter.Method), ("BatchId", filter.BatchId) })
        {
            if (!string.IsNullOrEmpty(value))
            {
                routeData.Values[key] = value;
            }
        }

        filter.List = new AdminListContext<Disbursement>
        {
            Source = disbursements,
            Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
        };
        filter.CanManage = await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements);
        filter.StatusOptions = StatusOptions(filter.Status);
        filter.MethodOptions =
        [
            new SelectListItem(S["All methods"], string.Empty),
            .. _disbursementService.Providers.Select(provider => new SelectListItem(provider.DisplayName, provider.Method, provider.Method == filter.Method)),
        ];
        filter.Options.BulkActions = filter.CanManage
            ? _disbursementService.Providers.Select(provider => new SelectListItem(S["Batch to send by {0}", provider.DisplayName], provider.Name)).ToList()
            : [];
        filter.List.ToolbarActions = await shapeFactory.CreateAsync<DisbursementsFiltersViewModel>("DisbursementsFilters", model =>
        {
            model.StatusOptions = filter.StatusOptions;
            model.MethodOptions = filter.MethodOptions;
        });

        return View(filter);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("banking/disbursements", "DisbursementsIndex")]
    public async Task<IActionResult> IndexFilterPost(DisbursementsIndexViewModel filter)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ViewDisbursements))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { nameof(filter.Search), filter.Search },
            { nameof(filter.Status), filter.Status },
            { nameof(filter.Method), filter.Method },
            { nameof(filter.BatchId), filter.BatchId },
        });
    }

    /// <summary>
    /// Puts the checked disbursements in a batch for the provider chosen in the list's bulk actions.
    /// </summary>
    /// <param name="itemIds">The checked disbursements.</param>
    /// <param name="filter">The list, with the chosen provider and the date the money should arrive.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("banking/disbursements", "DisbursementsIndex")]
    public async Task<IActionResult> IndexBulkPost(string[] itemIds, DisbursementsIndexViewModel filter)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        var ids = (itemIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var disbursements = await _manager.ListAsync(ids);
        var access = await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted);

        if (disbursements.Count != ids.Length || !disbursements.All(access.Allows))
        {
            return NotFound();
        }

        if (!DateOnly.TryParseExact(filter?.EffectiveDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveDate))
        {
            // No date chosen: the payments' own, such as a pay run's pay date, so nobody is paid early. A batch arrives
            // on one day: of payments due on different days, the last of them, never before tomorrow.
            var tomorrow = (await GetLocalTodayAsync()).AddDays(1);
            var dates = disbursements.Select(disbursement => disbursement.EffectiveDate).OfType<DateOnly>().Distinct().ToList();

            effectiveDate = dates.Count == 0 ? tomorrow : dates.Max() < tomorrow ? tomorrow : dates.Max();

            if (dates.Count > 1)
            {
                await _notifier.WarningAsync(H["The payments were due on different days, so the batch arrives on {0}, the last of them. Batch them apart to keep each day.", effectiveDate.ToString("d", CultureInfo.CurrentCulture)]);
            }
        }

        var (batch, problems) = await _disbursementService.CreateBatchAsync(filter?.Options?.BulkAction, ids, effectiveDate, HttpContext.RequestAborted);

        foreach (var problem in problems)
        {
            await _notifier.WarningAsync(H["{0}", problem]);
        }

        if (batch is not null)
        {
            await _notifier.SuccessAsync(H["A batch of {0} disbursements was made. <a href=\"{1}\">Open Disbursement Batches</a> to release it.", batch.Count, Url.Action(nameof(DisbursementBatchesController.Index), "DisbursementBatches")]);
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("banking/disbursements/create", "DisbursementsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        var draft = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.BuildEditorAsync(draft, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("banking/disbursements/create", "DisbursementsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        var draft = await _manager.NewAsync();
        var editor = await _displayManager.UpdateEditorAsync(draft, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            var result = await _disbursementService.CreateAsync(new DisbursementRequest
            {
                Payee = draft.Payee,
                PayeeName = draft.PayeeName,
                IsBusinessPayee = draft.IsBusinessPayee,
                Amount = draft.Amount,
                Method = draft.Method,
                SourceBankAccountId = draft.SourceBankAccountId,
                DestinationBankAccountId = draft.DestinationBankAccountId,
                Memo = draft.Memo,

                // Written by hand: each one is its own, so the key is the new record's id.
                IdempotencyKey = "Manual:" + draft.ItemId,
            }, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The disbursement to {0} has been recorded. Batch it to send it.", result.Disbursement.PayeeName]);

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        return View(new EditCatalogEntryViewModel { Editor = editor });
    }

    [HttpPost]
    [Admin("banking/disbursements/cancel/{id}", "DisbursementsCancel")]
    public async Task<IActionResult> Cancel(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } disbursement)
        {
            return NotFound();
        }

        if (await _disbursementService.CancelAsync(disbursement, string.IsNullOrWhiteSpace(reason) ? S["Cancelled"] : reason.Trim(), HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The disbursement to {0} was cancelled.", disbursement.PayeeName]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The disbursement to {0} is {1}: only a pending one can be cancelled. Cancel its open batch first.", disbursement.PayeeName, disbursement.Status.ToString()]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<Disbursement> FindManagedAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } disbursement)
        {
            return null;
        }

        return await _access.CanAccessAsync(User, disbursement.SourceKind, disbursement.AccessScope, SourceAccessLevel.Manage, HttpContext.RequestAborted) ? disbursement : null;
    }

    private async Task<DateOnly> GetLocalTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private List<SelectListItem> StatusOptions(string selected)
        =>
        [
            new SelectListItem(S["All statuses"], string.Empty),
            .. Enum.GetValues<DisbursementStatus>().Select(status => new SelectListItem(StatusText(status), status.ToString(), selected == status.ToString())),
        ];

    private string StatusText(DisbursementStatus status)
        => status switch
        {
            DisbursementStatus.Pending => S["Pending"],
            DisbursementStatus.Batched => S["Batched"],
            DisbursementStatus.Submitted => S["Submitted"],
            DisbursementStatus.Settled => S["Settled"],
            DisbursementStatus.Returned => S["Returned"],
            DisbursementStatus.Failed => S["Failed"],
            DisbursementStatus.Canceled => S["Cancelled"],
            _ => S["Reversed"],
        };
}
