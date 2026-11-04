using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
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
using OrchardCore.Routing;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// A worker's time clock page: their kiosk PIN, pay rates and attendance points.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class WorkerTimeClockController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ITimeClockService _timeClock;
    private readonly IAttendanceService _attendance;
    private readonly IActivityCodeManager _activityCodes;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerTimeClockController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="timeClock">The time clock.</param>
    /// <param name="attendance">The attendance service.</param>
    /// <param name="activityCodes">The activity codes.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkerTimeClockController(
        IAuthorizationService authorizationService,
        IWorkerManager workerManager,
        IWorkforceScopeService scopeService,
        ITimeClockService timeClock,
        IAttendanceService attendance,
        IActivityCodeManager activityCodes,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<WorkerTimeClockController> htmlLocalizer,
        IStringLocalizer<WorkerTimeClockController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _workerManager = workerManager;
        _scopeService = scopeService;
        _timeClock = timeClock;
        _attendance = attendance;
        _activityCodes = activityCodes;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows a worker's time clock page.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/workers/{workerId}/time-clock", "WorkforceWorkerTimeClock")]
    public async Task<IActionResult> Index(string workerId)
    {
        var (worker, denied) = await WorkerAsync(workerId);

        if (denied is not null)
        {
            return denied;
        }

        var today = await PlanningOptions.TodayAsync(_localClock);
        var settings = worker.TryGet<WorkerTimeClockSettings>(out var stored) ? stored : new WorkerTimeClockSettings();
        var rates = worker.TryGet<WorkerPayRates>(out var payRates) ? payRates : new WorkerPayRates();
        var activities = await _activityCodes.GetAllAsync(HttpContext.RequestAborted);
        var model = new WorkerTimeClockViewModel
        {
            Worker = worker,
            HasPin = !string.IsNullOrEmpty(settings.PinHash),
            LockedUntilUtc = settings.LockedUntilUtc,
            CanViewRates = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewPayRates),
            Policy = await _timeClock.GetPolicyAsync(worker, today, HttpContext.RequestAborted),
            Status = await _timeClock.GetStatusAsync(worker, HttpContext.RequestAborted),
            Points = await _attendance.GetPointsAsync(worker, today, HttpContext.RequestAborted),
            ActivityNames = activities.ToDictionary(code => code.ItemId, code => code.Name, StringComparer.Ordinal),
            ActivityOptions = activities.Select(code => new SelectListItem(code.Name, code.ItemId)).ToList(),
            RateFrom = today,
        };

        if (model.CanViewRates)
        {
            model.Rates = rates.Rates.OrderByDescending(rate => rate.EffectiveFrom).ToList();
        }

        return View(model);
    }

    /// <summary>
    /// Sets or removes the worker's kiosk PIN.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="model">The PIN.</param>
    /// <param name="remove">Whether to remove it.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("workforce/workers/{workerId}/time-clock/pin", "WorkforceWorkerTimeClockPin")]
    public async Task<IActionResult> Pin(string workerId, WorkerTimeClockViewModel model, bool remove = false)
    {
        var (worker, denied) = await WorkerAsync(workerId);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _timeClock.SetPinAsync(worker, remove ? null : model.Pin, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(remove ? H["The PIN has been removed."] : H["The PIN has been set. Give it to {0}.", worker.DisplayName]);
        }

        return RedirectToAction(nameof(Index), new { workerId });
    }

    /// <summary>
    /// Adds a pay rate.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="model">The rate.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("workforce/workers/{workerId}/time-clock/rates", "WorkforceWorkerTimeClockRates")]
    public async Task<IActionResult> AddRate(string workerId, WorkerTimeClockViewModel model)
    {
        var (worker, denied) = await WorkerAsync(workerId);

        if (denied is not null)
        {
            return denied;
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewPayRates))
        {
            return Forbid();
        }

        if (!model.RateFrom.HasValue || model.HourlyRate is not > 0)
        {
            await _notifier.ErrorAsync(H["Give the rate an hour and the day it starts."]);

            return RedirectToAction(nameof(Index), new { workerId });
        }

        var rates = worker.TryGet<WorkerPayRates>(out var stored) ? stored : new WorkerPayRates();
        var activity = string.IsNullOrEmpty(model.RateActivityCodeId) ? null : model.RateActivityCodeId;

        rates.Rates = rates.Rates.Where(rate => rate.EffectiveFrom != model.RateFrom.Value || rate.ActivityCodeId != activity).ToList();
        rates.Rates.Add(new WorkerPayRate { EffectiveFrom = model.RateFrom.Value, HourlyRate = Math.Round(model.HourlyRate.Value, 4), ActivityCodeId = activity });
        worker.Put(rates);
        await _workerManager.UpdateAsync(worker);
        await _notifier.SuccessAsync(H["The rate has been saved."]);

        return RedirectToAction(nameof(Index), new { workerId });
    }

    /// <summary>
    /// Removes a pay rate.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="from">The day the rate starts.</param>
    /// <param name="activityCodeId">The rate's activity, or empty.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("workforce/workers/{workerId}/time-clock/rates/remove", "WorkforceWorkerTimeClockRemoveRate")]
    public async Task<IActionResult> RemoveRate(string workerId, DateOnly from, string activityCodeId)
    {
        var (worker, denied) = await WorkerAsync(workerId);

        if (denied is not null)
        {
            return denied;
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewPayRates))
        {
            return Forbid();
        }

        var rates = worker.TryGet<WorkerPayRates>(out var stored) ? stored : new WorkerPayRates();
        var activity = string.IsNullOrEmpty(activityCodeId) ? null : activityCodeId;

        rates.Rates = rates.Rates.Where(rate => rate.EffectiveFrom != from || rate.ActivityCodeId != activity).ToList();
        worker.Put(rates);
        await _workerManager.UpdateAsync(worker);
        await _notifier.SuccessAsync(H["The rate has been removed."]);

        return RedirectToAction(nameof(Index), new { workerId });
    }

    private async Task<(WorkerProfile Worker, IActionResult Denied)> WorkerAsync(string workerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return (null, Forbid());
        }

        var worker = await _workerManager.FindByIdAsync(workerId);

        if (worker is null)
        {
            return (null, NotFound());
        }

        return await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted) ? (worker, null) : (null, Forbid());
    }
}
