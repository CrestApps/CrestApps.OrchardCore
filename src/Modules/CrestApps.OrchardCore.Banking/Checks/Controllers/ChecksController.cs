using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Checks.ViewModels;
using CrestApps.OrchardCore.Core.Models;
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

namespace CrestApps.OrchardCore.Banking.Checks.Controllers;

/// <summary>
/// Banking › Checks: the check register, checks written by hand, printing, voids, reissues, stops, cleared checks and
/// the next check number of each bank account.
/// </summary>
[Admin]
public sealed class ChecksController : Controller
{
    private readonly ICheckManager _manager;
    private readonly ICheckService _checkService;
    private readonly ICheckPrinter _printer;
    private readonly INamedCatalogManager<CheckLayout> _layouts;
    private readonly IBankAccountManager _bankAccounts;
    private readonly ICheckRecordStore _store;
    private readonly ISourceAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<CheckRecord> _displayManager;
    private readonly INotifier _notifier;
    private readonly ILocalClock _localClock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ChecksController(
        ICheckManager manager,
        ICheckService checkService,
        ICheckPrinter printer,
        INamedCatalogManager<CheckLayout> layouts,
        IBankAccountManager bankAccounts,
        ICheckRecordStore store,
        ISourceAccessService access,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<CheckRecord> displayManager,
        INotifier notifier,
        ILocalClock localClock,
        IHtmlLocalizer<ChecksController> htmlLocalizer,
        IStringLocalizer<ChecksController> stringLocalizer)
    {
        _manager = manager;
        _checkService = checkService;
        _printer = printer;
        _layouts = layouts;
        _bankAccounts = bankAccounts;
        _store = store;
        _access = access;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _localClock = localClock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("banking/checks", "ChecksIndex")]
    public async Task<IActionResult> Index(
        ChecksIndexViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ViewCheckRegister))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, checks) = await _store.ListPageAsync(
            new CheckRegisterQuery
            {
                BankAccountId = filter.BankAccountId,
                Status = Enum.TryParse<CheckStatus>(filter.Status, out var status) ? status : null,
                Search = filter.Search,
                Access = await _access.GetFilterAsync(User, SourceAccessLevel.View, HttpContext.RequestAborted),
            },
            pager.GetStartIndex(),
            pager.PageSize,
            HttpContext.RequestAborted);

        var routeData = new RouteData();

        foreach (var (key, value) in new[] { ("Search", filter.Search), ("BankAccountId", filter.BankAccountId), ("Status", filter.Status) })
        {
            if (!string.IsNullOrEmpty(value))
            {
                routeData.Values[key] = value;
            }
        }

        var accounts = await _bankAccounts.ListByOwnersAsync([default]);

        filter.List = new AdminListContext<CheckRecord>
        {
            Source = checks,
            Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
        };
        filter.CanManage = await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks);
        filter.BankAccountOptions = [new SelectListItem(S["All bank accounts"], string.Empty), .. accounts.Select(account => new SelectListItem(account.Name, account.ItemId, account.ItemId == filter.BankAccountId))];
        filter.StatusOptions = StatusOptions(filter.Status);
        filter.Options.BulkActions = filter.CanManage
            ? (await _layouts.GetAllAsync())
                .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
                .Select(layout => new SelectListItem(S["Print with {0}", layout.Name], layout.ItemId))
                .ToList()
            : [];
        filter.List.ToolbarActions = await shapeFactory.CreateAsync<ChecksRegisterFiltersViewModel>("ChecksRegisterFilters", model =>
        {
            model.BankAccountOptions = filter.BankAccountOptions;
            model.StatusOptions = filter.StatusOptions;
        });

        return View(filter);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("banking/checks", "ChecksIndex")]
    public async Task<IActionResult> IndexFilterPost(ChecksIndexViewModel filter)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ViewCheckRegister))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { nameof(filter.Search), filter.Search },
            { nameof(filter.BankAccountId), filter.BankAccountId },
            { nameof(filter.Status), filter.Status },
        });
    }

    [Admin("banking/checks/issue", "ChecksIssue")]
    public async Task<IActionResult> Issue()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var check = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.BuildEditorAsync(check, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Issue))]
    [Admin("banking/checks/issue", "ChecksIssue")]
    public async Task<IActionResult> IssuePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var draft = await _manager.NewAsync();
        var editor = await _displayManager.UpdateEditorAsync(draft, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            var result = await _checkService.IssueAsync(new CheckIssueRequest
            {
                BankAccountId = draft.BankAccountId,
                PayeeName = draft.PayeeName,
                PayeeAddress = draft.PayeeAddress,
                Amount = draft.Amount,
                CheckDate = draft.CheckDate,
                Memo = draft.Memo,
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["Check {0} has been issued. Print it from the register.", result.Check.Number]);

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        return View(new EditCatalogEntryViewModel { Editor = editor });
    }

    /// <summary>
    /// Prints the checked checks with the layout chosen in the list's bulk actions.
    /// </summary>
    /// <param name="itemIds">The checked checks.</param>
    /// <param name="filter">The list, with the chosen layout.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("banking/checks", "ChecksIndex")]
    public Task<IActionResult> IndexBulkPost(string[] itemIds, ChecksIndexViewModel filter)
        => Print(itemIds, filter?.Options?.BulkAction);

    /// <summary>
    /// Prints the checked checks with a layout to one PDF and records them as printed.
    /// </summary>
    /// <param name="itemIds">The checks.</param>
    /// <param name="layoutId">The layout.</param>
    [HttpPost]
    [Admin("banking/checks/print", "ChecksPrint")]
    public async Task<IActionResult> Print(string[] itemIds, string layoutId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var layout = string.IsNullOrEmpty(layoutId) ? null : await _layouts.FindByIdAsync(layoutId);

        if (layout is null)
        {
            await _notifier.ErrorAsync(H["Choose a check layout to print with."]);

            return RedirectToAction(nameof(Index));
        }

        var checks = await _manager.ListAsync(itemIds ?? []);

        if (checks.Count != (itemIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count() || !await CanManageAllAsync(checks))
        {
            return NotFound();
        }

        var printed = checks.Where(check => check.Status == CheckStatus.Printed).ToList();

        if (printed.Count > 0)
        {
            await _notifier.ErrorAsync(H["Check {0} is already printed. To print it again, choose Reprint in its menu and say why.", string.Join(", ", printed.Select(check => check.Number))]);

            return RedirectToAction(nameof(Index));
        }

        // The run's id is known before printing, so the Audit Trail entry of each account number read names it.
        var printRunId = IdGenerator.GenerateId();
        var result = await _printer.PrintAsync(layout, checks, printRunId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return RedirectToAction(nameof(Index));
        }

        await _checkService.MarkPrintedAsync(checks, printRunId);

        Response.Headers.CacheControl = "no-store";

        return File(result.Pdf, "application/pdf", $"checks-{checks[0].Number}-{checks[checks.Count - 1].Number}.pdf");
    }

    /// <summary>
    /// Asks which layout to print a printed check again with, and why.
    /// </summary>
    /// <param name="id">The check.</param>
    [Admin("banking/checks/reprint/{id}", "ChecksReprint")]
    public async Task<IActionResult> Reprint(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        return View(await ReprintViewModelAsync(new CheckReprintViewModel(), check));
    }

    /// <summary>
    /// Prints a printed check again, with the reason, and records the reprint.
    /// </summary>
    /// <param name="id">The check.</param>
    /// <param name="model">The layout and the reason.</param>
    [HttpPost]
    [ActionName(nameof(Reprint))]
    [Admin("banking/checks/reprint/{id}", "ChecksReprint")]
    public async Task<IActionResult> ReprintPost(string id, CheckReprintViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        var layout = string.IsNullOrEmpty(model.LayoutId) ? null : await _layouts.FindByIdAsync(model.LayoutId);

        if (layout is null)
        {
            ModelState.AddModelError(nameof(model.LayoutId), S["Choose the check layout to print with."]);
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), S["Say why the check is printed again, such as a printer jam."]);
        }

        if (check.Status != CheckStatus.Printed || check.IsHistorical)
        {
            ModelState.AddModelError(string.Empty, S["Only a printed check can be printed again."]);
        }

        if (!ModelState.IsValid)
        {
            return View(await ReprintViewModelAsync(model, check));
        }

        var printRunId = IdGenerator.GenerateId();
        var result = await _printer.PrintAsync(layout, [check], printRunId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(await ReprintViewModelAsync(model, check));
        }

        await _checkService.MarkReprintedAsync(check, printRunId, model.Reason);

        Response.Headers.CacheControl = "no-store";

        return File(result.Pdf, "application/pdf", $"check-{check.Number}-reprint.pdf");
    }

    [HttpPost]
    [Admin("banking/checks/void/{id}", "ChecksVoid")]
    public Task<IActionResult> Void(string id, string reason)
        => ChangeAsync(id, check => _checkService.VoidAsync(check, Reason(reason, S["Voided"])), H["Check voided. It keeps its number."]);

    [HttpPost]
    [Admin("banking/checks/stop/{id}", "ChecksStop")]
    public Task<IActionResult> Stop(string id, string reason)
        => ChangeAsync(id, check => _checkService.StopPaymentAsync(check, Reason(reason, S["Stop payment"])), H["Stop payment recorded. Ask the bank to stop the check if you have not."]);

    [HttpPost]
    [Admin("banking/checks/cleared/{id}", "ChecksCleared")]
    public Task<IActionResult> Cleared(string id, string clearedOn)
        => ChangeAsync(
            id,
            async check => await _checkService.MarkClearedAsync(check, DateOnly.TryParseExact(clearedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : await GetLocalTodayAsync()),
            H["The check is recorded as paid by the bank."]);

    [HttpPost]
    [Admin("banking/checks/reissue/{id}", "ChecksReissue")]
    public async Task<IActionResult> Reissue(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        var result = await _checkService.ReissueAsync(check, Reason(reason, S["Reissued"]));

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Check {0} replaces check {1}. Print the new check.", result.Check.Number, check.Number]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("banking/checks/next-number", "ChecksNextNumber")]
    public async Task<IActionResult> NextNumber()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        return View(await NextNumberViewModelAsync(new CheckNextNumberViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(NextNumber))]
    [Admin("banking/checks/next-number", "ChecksNextNumber")]
    public async Task<IActionResult> NextNumberPost(CheckNextNumberViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var account = string.IsNullOrEmpty(model.BankAccountId) ? null : await _bankAccounts.FindByIdAsync(model.BankAccountId);

        if (account is null || !account.Owner.IsEmpty)
        {
            ModelState.AddModelError(nameof(model.BankAccountId), S["Choose one of the business's bank accounts."]);
        }

        if (!long.TryParse(model.NextNumber?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var next) || next < 1)
        {
            ModelState.AddModelError(nameof(model.NextNumber), S["Enter the number printed on the first check of the new stock."]);
        }
        else if (ModelState.IsValid && !await _checkService.SetNextNumberAsync(account.ItemId, next))
        {
            ModelState.AddModelError(nameof(model.NextNumber), S["A check of this account already has number {0} or a later one.", next]);
        }

        if (!ModelState.IsValid)
        {
            return View(await NextNumberViewModelAsync(model));
        }

        await _notifier.SuccessAsync(H["The next check of {0} gets number {1}.", account.Name, next]);

        return RedirectToAction(nameof(NextNumber));
    }

    private async Task<IActionResult> ChangeAsync(string id, Func<CheckRecord, Task<bool>> change, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        if (await change(check))
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await _notifier.ErrorAsync(H["Check {0} is {1}: that can't be done.", check.Number, check.Status.ToString()]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Finds a check the user may act on; a check of a source the user may not see is not found.
    /// </summary>
    private async Task<CheckRecord> FindManagedAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || await _manager.FindByIdAsync(id) is not { } check)
        {
            return null;
        }

        return await _access.CanAccessAsync(User, check.SourceKind, check.AccessScope, SourceAccessLevel.Manage, HttpContext.RequestAborted) ? check : null;
    }

    private async Task<bool> CanManageAllAsync(IEnumerable<CheckRecord> checks)
    {
        var filter = await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted);

        return checks.All(check => filter.Allows(check.SourceKind, check.AccessScope));
    }

    private async Task<DateOnly> GetLocalTodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

    private async Task<CheckReprintViewModel> ReprintViewModelAsync(CheckReprintViewModel model, CheckRecord check)
    {
        model.Check = check;
        model.LayoutOptions = (await _layouts.GetAllAsync())
            .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
            .Select(layout => new SelectListItem(layout.Name, layout.ItemId, layout.ItemId == model.LayoutId))
            .ToList();

        return model;
    }

    private async Task<CheckNextNumberViewModel> NextNumberViewModelAsync(CheckNextNumberViewModel model)
    {
        var accounts = (await _bankAccounts.ListByOwnersAsync([default]))
            .Where(account => account.Status != BankAccountStatus.Closed)
            .ToList();

        model.BankAccountOptions = accounts.Select(account => new SelectListItem(account.Name, account.ItemId, account.ItemId == model.BankAccountId)).ToList();
        model.Current = [];

        foreach (var account in accounts)
        {
            model.Current.Add((account.Name, await _checkService.GetNextNumberAsync(account.ItemId)));
        }

        return model;
    }

    private List<SelectListItem> StatusOptions(string selected)
        =>
        [
            new SelectListItem(S["All statuses"], string.Empty),
            new SelectListItem(S["Issued"], nameof(CheckStatus.Issued), selected == nameof(CheckStatus.Issued)),
            new SelectListItem(S["Printed"], nameof(CheckStatus.Printed), selected == nameof(CheckStatus.Printed)),
            new SelectListItem(S["Cleared"], nameof(CheckStatus.Cleared), selected == nameof(CheckStatus.Cleared)),
            new SelectListItem(S["Voided"], nameof(CheckStatus.Voided), selected == nameof(CheckStatus.Voided)),
            new SelectListItem(S["Stop payment"], nameof(CheckStatus.StopPayment), selected == nameof(CheckStatus.StopPayment)),
            new SelectListItem(S["Reissued"], nameof(CheckStatus.Reissued), selected == nameof(CheckStatus.Reissued)),
            new SelectListItem(S["Stale"], nameof(CheckStatus.Stale), selected == nameof(CheckStatus.Stale)),
        ];

    private static string Reason(string reason, string fallback)
        => string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
}
