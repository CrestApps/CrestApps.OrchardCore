using System.Globalization;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Commerce &gt; Taxation &gt; Tax Returns: the periods each registration files for, by due date, and each return with
/// its figures, boxes, payments and actions. Preparing and marking ready need <c>ManageTaxReturns</c>; filing, amending
/// and payments need <c>FileTaxReturns</c>.
/// </summary>
[Admin]
[Feature(TaxationConstants.Feature.Returns)]
public sealed class TaxReturnsController : Controller
{
    private readonly ITaxReturnService _service;
    private readonly ITaxReturnManager _returns;
    private readonly IMerchantTaxRegistrationStore _registrations;
    private readonly IDisplayManager<TaxReturn> _displayManager;
    private readonly IDisplayManager<TaxReturnPaymentEntry> _paymentDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ISession _session;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public TaxReturnsController(
        ITaxReturnService service,
        ITaxReturnManager returns,
        IMerchantTaxRegistrationStore registrations,
        IDisplayManager<TaxReturn> displayManager,
        IDisplayManager<TaxReturnPaymentEntry> paymentDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        ILocalClock localClock,
        INotifier notifier,
        IStringLocalizerFactory localizerFactory,
        ISession session,
        ILogger<TaxReturnsController> logger,
        IHtmlLocalizer<TaxReturnsController> htmlLocalizer,
        IStringLocalizer<TaxReturnsController> stringLocalizer)
    {
        _service = service;
        _returns = returns;
        _registrations = registrations;
        _displayManager = displayManager;
        _paymentDisplayManager = paymentDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _localClock = localClock;
        _notifier = notifier;
        _localizerFactory = localizerFactory;
        _session = session;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("taxation/returns", "TaxationReturnsIndex")]
    public async Task<IActionResult> Index(int? year)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxReturns))
        {
            return Forbid();
        }

        var today = await TodayAsync();
        var chosenYear = year is > 1900 and < 3000 ? year.Value : today.Year;
        var periods = await _service.ListPeriodsAsync(new DateOnly(chosenYear, 1, 1), new DateOnly(chosenYear, 12, 31), HttpContext.RequestAborted);
        var list = new AdminListContext<TaxReturnPeriodSummary>
        {
            Source = periods,
            ToolbarActions = await _shapeFactory.CreateAsync<TaxReturnsYearFilterViewModel>("TaxReturnsYearFilter", model =>
            {
                model.Years =
                [
                    .. Enumerable.Range(today.Year - 6, 8)
                        .Reverse()
                        .Select(candidate => new SelectListItem(candidate.ToString(CultureInfo.CurrentCulture), candidate.ToString(CultureInfo.InvariantCulture), candidate == chosenYear)),
                ];
            }),
        };

        return View(new TaxReturnsIndexViewModel
        {
            Year = chosenYear,
            Periods = periods,
            HasFilingRegistrations = (await _registrations.GetAllAsync(HttpContext.RequestAborted)).Any(registration => registration.Filing is not null),
            List = list,
        });
    }

    [HttpPost]
    [Admin("taxation/returns/prepare", "TaxationReturnsPrepare")]
    public async Task<IActionResult> Prepare(string registrationId, string periodKey, bool annual = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxReturns))
        {
            return Forbid();
        }

        var result = await _service.PrepareAsync(registrationId, periodKey, annual, User, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);

            return result.Return is null
                ? RedirectToAction(nameof(Index), new { year = TaxReturnPeriod.Parse(periodKey)?.End.Year })
                : RedirectToAction(nameof(Details), new { id = result.Return.ItemId });
        }

        await _notifier.SuccessAsync(H["The return has been prepared from the figures of the period."]);

        return RedirectToAction(nameof(Details), new { id = result.Return.ItemId });
    }

    [Admin("taxation/returns/{id}", "TaxationReturnsDetails")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxReturns))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } taxReturn)
        {
            return NotFound();
        }

        var registration = await _registrations.FindByIdAsync(taxReturn.RegistrationId, HttpContext.RequestAborted);
        var formName = _service.GetForms().TryGetValue(taxReturn.Form ?? string.Empty, out var form)
            ? _localizerFactory.Localize(form.DisplayName).Value
            : taxReturn.Form;
        var title = taxReturn.Amendment > 0
            ? S["{0} {1}, amendment {2}", formName, taxReturn.PeriodKey, taxReturn.Amendment]
            : S["{0} {1}", formName, taxReturn.PeriodKey];

        return View(new TaxReturnDetailsViewModel
        {
            Return = taxReturn,
            Title = registration is null ? title : S["{0} · {1}", registration.Name, title],
            Display = await _displayManager.BuildDisplayAsync(taxReturn, _updateModelAccessor.ModelUpdater, "DetailAdmin"),
            Today = await TodayAsync(),
            CanFile = await _authorizationService.AuthorizeAsync(User, TaxationPermissions.FileTaxReturns),
        });
    }

    [HttpPost]
    [Admin("taxation/returns/{id}/prepare", "TaxationReturnsPrepareAgain")]
    public Task<IActionResult> PrepareAgain(string id)
        => ActAsync(id, TaxationPermissions.ManageTaxReturns, (taxReturn, cancellationToken) => _service.PrepareAgainAsync(taxReturn, User, cancellationToken), H["The return has been prepared again."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/ready", "TaxationReturnsReady")]
    public Task<IActionResult> MarkReady(string id)
        => ActAsync(id, TaxationPermissions.ManageTaxReturns, (taxReturn, cancellationToken) => _service.MarkReadyAsync(taxReturn, User, cancellationToken), H["The return is ready to file."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/reopen", "TaxationReturnsReopen")]
    public Task<IActionResult> Reopen(string id)
        => ActAsync(id, TaxationPermissions.ManageTaxReturns, (taxReturn, cancellationToken) => _service.ReopenAsync(taxReturn, User, cancellationToken), H["The return is a draft again."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/filed", "TaxationReturnsFiled")]
    public Task<IActionResult> MarkFiled(string id, DateOnly? filedOn, string confirmationNumber)
        => ActAsync(id, TaxationPermissions.FileTaxReturns, (taxReturn, cancellationToken) => _service.MarkFiledAsync(taxReturn, filedOn ?? default, confirmationNumber, channel: null, User, cancellationToken), H["The return is marked as filed."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/amend", "TaxationReturnsAmend")]
    public Task<IActionResult> Amend(string id)
        => ActAsync(id, TaxationPermissions.FileTaxReturns, (taxReturn, cancellationToken) => _service.AmendAsync(taxReturn, User, cancellationToken), H["An amended return has been prepared: check it, file it, then mark it as filed."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/payments", "TaxationReturnsRecordPayment")]
    public async Task<IActionResult> RecordPayment(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.FileTaxReturns))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } taxReturn)
        {
            return NotFound();
        }

        // The payment form is built from drivers: each one reads its own fields, such as a pack's account paid from.
        var entry = new TaxReturnPaymentEntry { Return = taxReturn };

        await _paymentDisplayManager.UpdateEditorAsync(entry, _updateModelAccessor.ModelUpdater, isNew: true);

        // Recording the payment saves the return, so the return's own rules run first, as for any other write.
        await CatalogEntryValidation.ValidateAsync(_returns, taxReturn, _updateModelAccessor.ModelUpdater, nameof(TaxReturn), HttpContext.RequestAborted);

        if (!ModelState.IsValid)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage))]);

            return RedirectToAction(nameof(Details), new { id });
        }

        return await RunAsync(taxReturn, (current, cancellationToken) => _service.RecordPaymentAsync(current, entry.Payment, User, cancellationToken), H["The payment has been recorded."]);
    }

    [HttpPost]
    [Admin("taxation/returns/{id}/payments/{paymentId}/delete", "TaxationReturnsRemovePayment")]
    public Task<IActionResult> RemovePayment(string id, string paymentId)
        => ActAsync(id, TaxationPermissions.FileTaxReturns, (taxReturn, cancellationToken) => _service.RemovePaymentAsync(taxReturn, paymentId, User, cancellationToken), H["The payment has been removed."]);

    [HttpPost]
    [Admin("taxation/returns/{id}/delete", "TaxationReturnsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxReturns))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } taxReturn)
        {
            return RedirectToAction(nameof(Index));
        }

        // A return ready, filed or amended is a record of what the business filed, and a draft with payments holds them.
        if (taxReturn.Status != TaxReturnStatus.Draft || taxReturn.Payments.Count > 0)
        {
            await _notifier.ErrorAsync(H["Only a draft with no payment is removed."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _returns.DeleteAsync(taxReturn, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The draft has been removed."]);

        return RedirectToAction(nameof(Index), new { year = taxReturn.PeriodEnd.Year });
    }

    private async Task<IActionResult> ActAsync(string id, Permission permission, Func<TaxReturn, CancellationToken, Task<TaxReturnResult>> action, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, permission))
        {
            return Forbid();
        }

        if (await FindAsync(id) is not { } taxReturn)
        {
            return NotFound();
        }

        return await RunAsync(taxReturn, action, success);
    }

    // A module that follows tax returns (an ITaxReturnObserver, such as one that posts the payment to the books) may fail:
    // nothing of the change is saved, and the person is told instead of getting an error page.
    private async Task<IActionResult> RunAsync(TaxReturn taxReturn, Func<TaxReturn, CancellationToken, Task<TaxReturnResult>> action, LocalizedHtmlString success)
    {
        TaxReturnResult result;

        try
        {
            result = await action(taxReturn, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _session.CancelAsync();
            _logger.LogError(exception, "A change to the tax return '{TaxReturnId}' failed, so nothing of it was saved.", taxReturn.ItemId);
            await _notifier.ErrorAsync(H["The change was not saved: a module that acts on tax returns failed. The error has been logged."]);

            return RedirectToAction(nameof(Details), new { id = taxReturn.ItemId });
        }

        await (result.Succeeded
            ? _notifier.SuccessAsync(success)
            : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return RedirectToAction(nameof(Details), new { id = result.Return?.ItemId ?? taxReturn.ItemId });
    }

    private async Task<TaxReturn> FindAsync(string id)
        => string.IsNullOrEmpty(id) ? null : await _returns.FindByIdAsync(id, HttpContext.RequestAborted);

    private async Task<DateOnly> TodayAsync()
        => DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
}
