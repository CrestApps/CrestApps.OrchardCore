using System.Globalization;
using CrestApps.Core;
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
/// Banking › Checks: the check register, checks written in the admin (numbered now, at print, or by hand), edits of
/// checks that wait to print, printing,
/// print runs of queued checks on pre-printed stock, voids on a date, reissues, stops, cleared and un-cleared checks,
/// and the next check number of each bank account.
/// </summary>
[Admin]
[Feature(ChecksConstants.Features.Checks)]
public sealed class ChecksController : Controller
{
    /// <summary>
    /// The bulk action that prints with the default layout of the checks' bank account.
    /// </summary>
    public const string DefaultLayout = "default";

    private const int PrintRunPageSize = 500;

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
    private readonly IDisplayManager<CheckVoidViewModel> _voidDisplayManager;
    private readonly IDisplayManager<CheckReprintViewModel> _reprintDisplayManager;
    private readonly IDisplayManager<CheckNextNumberViewModel> _nextNumberDisplayManager;
    private readonly IDisplayManager<CheckPrintRunViewModel> _printRunDisplayManager;
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
        IDisplayManager<CheckVoidViewModel> voidDisplayManager,
        IDisplayManager<CheckReprintViewModel> reprintDisplayManager,
        IDisplayManager<CheckNextNumberViewModel> nextNumberDisplayManager,
        IDisplayManager<CheckPrintRunViewModel> printRunDisplayManager,
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
        _voidDisplayManager = voidDisplayManager;
        _reprintDisplayManager = reprintDisplayManager;
        _nextNumberDisplayManager = nextNumberDisplayManager;
        _printRunDisplayManager = printRunDisplayManager;
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
            ?
            [
                new SelectListItem(S["Print with the account's default layout"], DefaultLayout),
                .. (await _layouts.GetAllAsync())
                    .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(layout => new SelectListItem(S["Print with {0}", layout.Name], layout.ItemId)),
            ]
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

        // Another module's editor may write the check for one of its own records; the user must be allowed to act on it.
        if (!string.IsNullOrEmpty(draft.SourceKind)
            && !await _access.CanAccessAsync(User, draft.SourceKind, draft.AccessScope, SourceAccessLevel.Manage, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            // What other modules' drivers put on the draft (the party paid, their source, their own data) goes with it;
            // their ICheckWriteHandler completes, refuses or posts it.
            var result = await _checkService.IssueAsync(new CheckIssueRequest
            {
                BankAccountId = draft.BankAccountId,
                Payee = draft.Payee,
                PayeeName = draft.PayeeName,
                PayeeAddress = draft.PayeeAddress,
                Amount = draft.Amount,
                CheckDate = draft.CheckDate,
                Memo = draft.Memo,
                SourceKind = string.IsNullOrEmpty(draft.SourceKind) ? null : draft.SourceKind,
                SourceId = string.IsNullOrEmpty(draft.SourceId) ? null : draft.SourceId,
                AccessScope = draft.AccessScope,
                Properties = draft.Properties,
                Numbering = draft.Numbering,
                Number = draft.Numbering == CheckNumbering.Handwritten ? draft.Number : null,
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(result.Check.Numbering switch
                {
                    CheckNumbering.AtPrint => H["The check to {0} waits to print. Print it with Print queued checks: it gets the number of the sheet it prints on.", result.Check.PayeeName],
                    CheckNumbering.Handwritten => H["Check {0}, written by hand, is recorded.", result.Check.Number],
                    _ => H["Check {0} has been issued. Print it from the register.", result.Check.Number],
                });

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
    /// Shows the editor of a check that waits to print: its payee, address, amount, date and memo, and what other
    /// modules' drivers add.
    /// </summary>
    /// <param name="id">The check.</param>
    [Admin("banking/checks/edit/{id}", "ChecksEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        if (!CheckStatuses.CanEdit(check))
        {
            await _notifier.ErrorAsync(H["Only a check that waits to print can be edited. Void it and write a new check instead."]);

            return RedirectToAction(nameof(Index));
        }

        return View(new CheckEditViewModel
        {
            Check = check,
            Editor = await _displayManager.BuildEditorAsync(DraftOf(check), _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a check that waits to print. The module the check pays may refuse the change, or follow it.
    /// </summary>
    /// <param name="id">The check.</param>
    /// <param name="model">Why it changes.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("banking/checks/edit/{id}", "ChecksEdit")]
    public async Task<IActionResult> EditPost(string id, CheckEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        if (!CheckStatuses.CanEdit(check))
        {
            await _notifier.ErrorAsync(H["Only a check that waits to print can be edited. Void it and write a new check instead."]);

            return RedirectToAction(nameof(Index));
        }

        // The editors change a copy; the check rules save it, or say why not.
        var draft = DraftOf(check);
        var editor = await _displayManager.UpdateEditorAsync(draft, _updateModelAccessor.ModelUpdater, isNew: false);

        if (ModelState.IsValid)
        {
            var result = await _checkService.UpdateAsync(
                check,
                new CheckDetails
                {
                    Payee = draft.Payee,
                    PayeeName = draft.PayeeName,
                    PayeeAddress = draft.PayeeAddress,
                    Amount = draft.Amount,
                    CheckDate = draft.CheckDate,
                    Memo = draft.Memo,
                    Properties = draft.Properties,
                },
                string.IsNullOrWhiteSpace(model?.Reason) ? null : model.Reason.Trim());

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(check.Number > 0
                    ? H["Check {0} has been saved.", check.Number]
                    : H["The check to {0} has been saved.", check.PayeeName]);

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        return View(nameof(Edit), new CheckEditViewModel { Check = check, Editor = editor, Reason = model?.Reason });
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
    /// Prints the checked checks with a layout, or the default layout of their bank account, to one PDF and records
    /// them as printed. Queued checks go to a print run instead.
    /// </summary>
    /// <param name="itemIds">The checks.</param>
    /// <param name="layoutId">The layout, or <see cref="DefaultLayout"/> (or empty) for the account's default layout.</param>
    [HttpPost]
    [Admin("banking/checks/print", "ChecksPrint")]
    public async Task<IActionResult> Print(string[] itemIds, string layoutId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var checks = await _manager.ListAsync(itemIds ?? []);

        if (checks.Count != (itemIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count() || !await CanManageAllAsync(checks))
        {
            return NotFound();
        }

        // Queued checks are numbered by a print run, one bank account at a time.
        if (checks.Count > 0 && checks.All(check => check.Status == CheckStatus.Queued) && checks.Select(check => check.BankAccountId).Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return RedirectToAction(nameof(PrintRun), new { bankAccountId = checks[0].BankAccountId, layoutId = string.Equals(layoutId, DefaultLayout, StringComparison.Ordinal) ? null : layoutId });
        }

        var layout = await ResolveLayoutAsync(layoutId, checks);

        if (layout is null)
        {
            return RedirectToAction(nameof(Index));
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
    /// Lists the checks of a bank account that wait for a print run on pre-printed stock, with the layout (the account's
    /// default when it has one) and the number of the account's next check as the first sheet's number.
    /// </summary>
    /// <param name="bankAccountId">The bank account.</param>
    /// <param name="layoutId">The layout, when one was already chosen.</param>
    [Admin("banking/checks/print-run", "ChecksPrintRun")]
    public async Task<IActionResult> PrintRun(string bankAccountId, string layoutId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var model = new CheckPrintRunViewModel { BankAccountId = bankAccountId, LayoutId = layoutId };
        var account = await FindBusinessAccountAsync(bankAccountId);

        if (account is not null)
        {
            model.FirstNumber = (await _checkService.GetNextNumberAsync(account.ItemId)).ToString(CultureInfo.InvariantCulture);
            model.LayoutId ??= DefaultLayoutIdOf(account);
        }

        return View(await PrintRunViewModelAsync(model, account, selectAll: true));
    }

    /// <summary>
    /// Checks the chosen queued checks, the layout and the first number, and shows the page that prints the run and asks
    /// which sheets misprinted.
    /// </summary>
    /// <param name="model">The run.</param>
    [HttpPost]
    [ActionName(nameof(PrintRun))]
    [Admin("banking/checks/print-run", "ChecksPrintRun")]
    public async Task<IActionResult> PrintRunPost(CheckPrintRunViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var account = await FindBusinessAccountAsync(model.BankAccountId);
        var (checks, layout, first) = await ReadRunAsync(model, account);

        if (checks is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await PrintRunViewModelAsync(model, account, selectAll: false));
        }

        model.CheckIds = checks.Select(check => check.ItemId).ToArray();
        model.PrintRunId = IdGenerator.GenerateId();
        model.Checks = checks;
        model.First = first;
        model.BankAccountName = account.Name;
        model.LayoutOptions = [new SelectListItem(layout.Name, layout.ItemId, true)];

        return View("PrintRunConfirm", model);
    }

    /// <summary>
    /// Prints the run's checks to one PDF with the numbers of the sheets they print on. Nothing is saved: the run is
    /// recorded when the person confirms it.
    /// </summary>
    /// <param name="model">The run.</param>
    [HttpPost]
    [Admin("banking/checks/print-run/pdf", "ChecksPrintRunPdf")]
    public async Task<IActionResult> PrintRunPdf(CheckPrintRunViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var account = await FindBusinessAccountAsync(model.BankAccountId);
        var (checks, layout, first) = await ReadRunAsync(model, account);

        if (checks is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            foreach (var error in ModelState.Values.SelectMany(entry => entry.Errors))
            {
                await _notifier.ErrorAsync(H["{0}", error.ErrorMessage]);
            }

            return RedirectToAction(nameof(PrintRun), new { bankAccountId = model.BankAccountId });
        }

        var result = await _printer.PrintAsync(layout, checks, string.IsNullOrEmpty(model.PrintRunId) ? IdGenerator.GenerateId() : model.PrintRunId, first, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return RedirectToAction(nameof(PrintRun), new { bankAccountId = model.BankAccountId });
        }

        Response.Headers.CacheControl = "no-store";

        return File(result.Pdf, "application/pdf", $"checks-{first}-{first + checks.Count - 1}.pdf");
    }

    /// <summary>
    /// Records the run once the person said which sheets misprinted: the checks get their sheets' numbers and are
    /// printed, each misprinted sheet is kept as a spoiled check, and its check waits to print again.
    /// </summary>
    /// <param name="model">The run, with the misprinted sheets.</param>
    [HttpPost]
    [Admin("banking/checks/print-run/confirm", "ChecksConfirmPrintRun")]
    public async Task<IActionResult> ConfirmPrintRun(CheckPrintRunViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        var account = await FindBusinessAccountAsync(model.BankAccountId);
        var (checks, _, first) = await ReadRunAsync(model, account);

        if (checks is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            foreach (var error in ModelState.Values.SelectMany(entry => entry.Errors))
            {
                await _notifier.ErrorAsync(H["{0}", error.ErrorMessage]);
            }

            return RedirectToAction(nameof(PrintRun), new { bankAccountId = model.BankAccountId });
        }

        var result = await _checkService.ConfirmPrintRunAsync(new CheckPrintRunRequest
        {
            Checks = checks,
            FirstNumber = first,
            MisprintedCheckIds = model.MisprintedCheckIds ?? [],
            PrintRunId = model.PrintRunId,
        });

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }

            return RedirectToAction(nameof(PrintRun), new { bankAccountId = model.BankAccountId });
        }

        await _notifier.SuccessAsync(H["{0} checks are recorded as printed.", result.Printed.Count]);

        if (result.Spoiled.Count > 0)
        {
            await _notifier.WarningAsync(H["Sheets {0} misprinted: their numbers are kept as spoiled checks, and their checks wait to print again.", string.Join(", ", result.Spoiled.Select(check => check.Number))]);

            return RedirectToAction(nameof(PrintRun), new { bankAccountId = model.BankAccountId });
        }

        return RedirectToAction(nameof(Index));
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

        return View(await ReprintViewModelAsync(new CheckReprintViewModel { LayoutId = await DefaultLayoutIdAsync(check) }, check));
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

        if (check.Status != CheckStatus.Printed || check.IsHistorical || check.Numbering == CheckNumbering.Handwritten)
        {
            ModelState.AddModelError(string.Empty, S["Only a check the site printed can be printed again."]);
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

    /// <summary>
    /// Asks the void date and why a check is voided.
    /// </summary>
    /// <param name="id">The check.</param>
    [Admin("banking/checks/void/{id}", "ChecksVoid")]
    public async Task<IActionResult> Void(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        var today = await GetLocalTodayAsync();

        return View(await VoidEditorAsync(new CheckVoidViewModel
        {
            Check = check,
            VoidedOn = (check.CheckDate > today ? check.CheckDate : today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }));
    }

    /// <summary>
    /// Voids a check on a date: today in the site's time zone when none is given. The module the check pays may refuse.
    /// </summary>
    /// <param name="id">The check.</param>
    /// <param name="reason">Why.</param>
    /// <param name="voidedOn">The void date, as yyyy-MM-dd.</param>
    [HttpPost]
    [Admin("banking/checks/void/{id}", "ChecksVoid")]
    public async Task<IActionResult> Void(string id, string reason, string voidedOn = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        var hasDate = DateOnly.TryParseExact(voidedOn?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);

        if (!hasDate && !string.IsNullOrWhiteSpace(voidedOn))
        {
            ModelState.AddModelError(nameof(CheckVoidViewModel.VoidedOn), S["Enter the void date."]);

            return View(await VoidEditorAsync(new CheckVoidViewModel { Check = check, Reason = reason, VoidedOn = voidedOn }));
        }

        var result = hasDate
            ? await _checkService.VoidAsync(check, Reason(reason, S["Voided"]), date)
            : await VoidTodayAsync(check, Reason(reason, S["Voided"]));

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Check voided on {0}. It keeps its number.", check.VoidedOn?.ToString("d", CultureInfo.CurrentCulture)]);

            return RedirectToAction(nameof(Index));
        }

        // The void page shows why; a post from elsewhere goes back to the register with the reasons.
        if (!string.IsNullOrEmpty(voidedOn))
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(await VoidEditorAsync(new CheckVoidViewModel { Check = check, Reason = reason, VoidedOn = voidedOn }));
        }

        await NotifyErrorsAsync(result.Errors);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("banking/checks/stop/{id}", "ChecksStop")]
    public Task<IActionResult> Stop(string id, string reason)
        => ChangeAsync(
            id,
            async check => await _checkService.StopPaymentAsync(check, Reason(reason, S["Stop payment"])) ? new CheckResult { Check = check } : Refused(check),
            H["Stop payment recorded. Ask the bank to stop the check if you have not."]);

    [HttpPost]
    [Admin("banking/checks/cleared/{id}", "ChecksCleared")]
    public Task<IActionResult> Cleared(string id, string clearedOn)
        => ChangeAsync(
            id,
            async check => await _checkService.ClearAsync(check, DateOnly.TryParseExact(clearedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : await GetLocalTodayAsync()),
            H["The check is recorded as paid by the bank."]);

    /// <summary>
    /// Records that a cleared check was not paid after all: it goes back to the status it cleared from.
    /// </summary>
    /// <param name="id">The check.</param>
    /// <param name="reason">Why.</param>
    [HttpPost]
    [Admin("banking/checks/unclear/{id}", "ChecksUnclear")]
    public Task<IActionResult> Unclear(string id, string reason)
        => ChangeAsync(
            id,
            check => _checkService.UnclearAsync(check, Reason(reason, S["Not paid by the bank after all"])),
            H["The check is no longer recorded as paid by the bank."]);

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
            await _notifier.SuccessAsync(result.Check.Status == CheckStatus.Queued
                ? H["A new check replaces check {0} and waits to print.", check.Number]
                : H["Check {0} replaces check {1}. Print the new check.", result.Check.Number, check.Number]);
        }
        else
        {
            await NotifyErrorsAsync(result.Errors);
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

    // A copy of a check for its editors, so a refused or invalid edit leaves the check as it is.
    private static CheckRecord DraftOf(CheckRecord check)
        => new()
        {
            ItemId = check.ItemId,
            BankAccountId = check.BankAccountId,
            BankAccountName = check.BankAccountName,
            Number = check.Number,
            Numbering = check.Numbering,
            Payee = check.Payee,
            PayeeName = check.PayeeName,
            PayeeAddress = check.PayeeAddress,
            Amount = check.Amount,
            Currency = check.Currency,
            CheckDate = check.CheckDate,
            Memo = check.Memo,
            SourceKind = check.SourceKind,
            SourceId = check.SourceId,
            AccessScope = check.AccessScope,
            Status = check.Status,
            IssuedUtc = check.IssuedUtc,
            IdempotencyKey = check.IdempotencyKey,
            Properties = new Dictionary<string, object>(check.Properties, StringComparer.OrdinalIgnoreCase),
        };

    private async Task<IActionResult> ChangeAsync(string id, Func<CheckRecord, Task<CheckResult>> change, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ChecksPermissions.ManageChecks))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } check)
        {
            return NotFound();
        }

        var result = await change(check);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await NotifyErrorsAsync(result.Errors.Count > 0 ? result.Errors : Refused(check).Errors);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CheckResult> VoidTodayAsync(CheckRecord check, string reason)
        => await _checkService.VoidAsync(check, reason) ? new CheckResult { Check = check } : Refused(check);

    private CheckResult Refused(CheckRecord check)
        => new() { Errors = [S["Check {0} is {1}: that can't be done.", check.Number, check.Status.ToString()]] };

    private async Task NotifyErrorsAsync(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }

    /// <summary>
    /// Reads a print run from the posted form: the queued checks in the posted order, the layout and the first number.
    /// Returns no checks when a check isn't found or the user may not act on it; adds model errors for the rest.
    /// </summary>
    private async Task<(IReadOnlyList<CheckRecord> Checks, CheckLayout Layout, long First)> ReadRunAsync(CheckPrintRunViewModel model, BankAccount account)
    {
        var ids = (model.CheckIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        var found = (await _manager.ListAsync(ids)).ToDictionary(check => check.ItemId, StringComparer.Ordinal);

        if (found.Count != ids.Count || !await CanManageAllAsync(found.Values))
        {
            return (null, null, 0);
        }

        var checks = ids.Select(id => found[id]).ToList();

        if (account is null)
        {
            ModelState.AddModelError(nameof(model.BankAccountId), S["Choose one of the business's bank accounts."]);
        }
        else if (checks.Any(check => !string.Equals(check.BankAccountId, account.ItemId, StringComparison.Ordinal)))
        {
            ModelState.AddModelError(nameof(model.CheckIds), S["A print run on pre-printed stock prints the checks of one bank account."]);
        }

        var layout = string.IsNullOrEmpty(model.LayoutId) ? null : await _layouts.FindByIdAsync(model.LayoutId);

        if (layout is null)
        {
            ModelState.AddModelError(nameof(model.LayoutId), S["Choose the check layout to print with."]);
        }

        if (!long.TryParse(model.FirstNumber?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var first) || first < 1)
        {
            ModelState.AddModelError(nameof(model.FirstNumber), S["Enter the number printed on the first sheet."]);
        }
        else if (checks.Count == 0)
        {
            ModelState.AddModelError(nameof(model.CheckIds), S["Choose the checks to print."]);
        }
        else if (ModelState.IsValid)
        {
            foreach (var error in await _checkService.ValidatePrintRunAsync(checks, first))
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        return (checks, layout, first);
    }

    private async Task<CheckPrintRunViewModel> PrintRunViewModelAsync(CheckPrintRunViewModel model, BankAccount account, bool selectAll)
    {
        var accounts = (await _bankAccounts.ListByOwnersAsync([default]))
            .Where(entry => entry.Status != BankAccountStatus.Closed)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        model.BankAccountOptions = [new SelectListItem(S["Choose a bank account"], string.Empty), .. accounts.Select(entry => new SelectListItem(entry.Name, entry.ItemId, entry.ItemId == model.BankAccountId))];
        model.LayoutOptions = (await _layouts.GetAllAsync())
            .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
            .Select(layout => new SelectListItem(layout.Name, layout.ItemId, layout.ItemId == model.LayoutId))
            .ToList();
        model.BankAccountName = account?.Name;
        model.Checks = [];

        if (account is not null)
        {
            var (_, queued) = await _store.ListPageAsync(
                new CheckRegisterQuery
                {
                    BankAccountId = account.ItemId,
                    Status = CheckStatus.Queued,
                    OldestFirst = true,
                    Access = await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted),
                },
                0,
                PrintRunPageSize,
                HttpContext.RequestAborted);

            model.Checks = queued;

            if (selectAll)
            {
                model.CheckIds = queued.Select(check => check.ItemId).ToArray();
            }
        }

        model.Editor = await _printRunDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return model;
    }

    // Finds a layout by id, or the default layout of the checks' bank account; says why when there is none.
    private async Task<CheckLayout> ResolveLayoutAsync(string layoutId, IReadOnlyList<CheckRecord> checks)
    {
        if (!string.IsNullOrEmpty(layoutId) && !string.Equals(layoutId, DefaultLayout, StringComparison.Ordinal))
        {
            if (await _layouts.FindByIdAsync(layoutId) is { } chosen)
            {
                return chosen;
            }

            await _notifier.ErrorAsync(H["Choose a check layout to print with."]);

            return null;
        }

        var defaults = new HashSet<string>(StringComparer.Ordinal);

        foreach (var accountId in checks.Select(check => check.BankAccountId).Distinct(StringComparer.Ordinal))
        {
            var account = await _bankAccounts.FindByIdAsync(accountId);
            var defaultId = account is null ? null : DefaultLayoutIdOf(account);

            if (string.IsNullOrEmpty(defaultId))
            {
                await _notifier.ErrorAsync(H["{0} has no default check layout. Choose a layout to print with, or set one on the bank account.", account?.Name ?? accountId]);

                return null;
            }

            defaults.Add(defaultId);
        }

        if (defaults.Count != 1)
        {
            await _notifier.ErrorAsync(H["The checks' bank accounts print with different layouts. Print each account's checks on their own, or choose a layout."]);

            return null;
        }

        var layout = await _layouts.FindByIdAsync(defaults.First());

        if (layout is null)
        {
            await _notifier.ErrorAsync(H["The default check layout of the bank account no longer exists. Choose another on the bank account."]);
        }

        return layout;
    }

    private async Task<string> DefaultLayoutIdAsync(CheckRecord check)
        => await _bankAccounts.FindByIdAsync(check.BankAccountId) is { } account ? DefaultLayoutIdOf(account) : null;

    private static string DefaultLayoutIdOf(BankAccount account)
        => account.TryGet<BankAccountCheckSettings>(out var settings) && !string.IsNullOrEmpty(settings.DefaultLayoutId) ? settings.DefaultLayoutId : null;

    private async Task<BankAccount> FindBusinessAccountAsync(string bankAccountId)
        => string.IsNullOrEmpty(bankAccountId) || await _bankAccounts.FindByIdAsync(bankAccountId) is not { } account || !account.Owner.IsEmpty
            ? null
            : account;

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

    private async Task<CheckVoidViewModel> VoidEditorAsync(CheckVoidViewModel model)
    {
        model.Editor = await _voidDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return model;
    }

    private async Task<CheckReprintViewModel> ReprintViewModelAsync(CheckReprintViewModel model, CheckRecord check)
    {
        model.Check = check;
        model.LayoutOptions = (await _layouts.GetAllAsync())
            .OrderBy(layout => layout.Name, StringComparer.OrdinalIgnoreCase)
            .Select(layout => new SelectListItem(layout.Name, layout.ItemId, layout.ItemId == model.LayoutId))
            .ToList();
        model.Editor = await _reprintDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

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

        model.Editor = await _nextNumberDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return model;
    }

    private List<SelectListItem> StatusOptions(string selected)
        =>
        [
            new SelectListItem(S["All statuses"], string.Empty),
            new SelectListItem(S["Waiting to print"], nameof(CheckStatus.Queued), selected == nameof(CheckStatus.Queued)),
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
