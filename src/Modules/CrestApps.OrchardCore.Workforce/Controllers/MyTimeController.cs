using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The time clock in the My Work portal: clocking in and out, the worker's timesheet, submitting it, and asking for
/// a missed punch. It needs the Time Clock feature too.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
public sealed class MyTimeController : Controller
{
    private const string Attestation = "I confirm these hours are complete and correct.";

    private readonly IAuthorizationService _authorizationService;
    private readonly MyWorkPortal _portal;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTimeController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="portal">The portal helper.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public MyTimeController(
        IAuthorizationService authorizationService,
        MyWorkPortal portal,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<MyTimeController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _portal = portal;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Records the worker's punch.
    /// </summary>
    /// <param name="kind">The punch.</param>
    /// <param name="latitude">Where, when shared.</param>
    /// <param name="longitude">Where, when shared.</param>
    /// <param name="key">A key that makes a punch sent twice count once.</param>
    /// <param name="source">Web, or Mobile from the installed app.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>A redirect back.</returns>
    [HttpPost("my-work/punch")]
    public async Task<IActionResult> Punch(TimePunchKind kind, double? latitude, double? longitude, string key, string source, string returnUrl)
    {
        var (worker, timeClock, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var result = await timeClock.PunchAsync(new TimePunchRequest
        {
            WorkerId = worker.ItemId,
            Kind = kind,
            Source = source == nameof(TimePunchSource.Mobile) ? TimePunchSource.Mobile : TimePunchSource.Web,
            Latitude = latitude,
            Longitude = longitude,
            IdempotencyKey = string.IsNullOrWhiteSpace(key) ? null : "portal:" + key,
            DeviceId = Request.Headers.UserAgent.ToString(),
        }, User.Identity?.Name, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            var zone = (await timeClock.GetStatusAsync(worker, HttpContext.RequestAborted)).TimeZoneId ?? "UTC";
            var time = CalendarTime.ToLocal(result.Value.Utc, zone).ToString("t", CultureInfo.CurrentCulture);

            await _notifier.SuccessAsync(kind switch
            {
                TimePunchKind.In => H["Clocked in at {0}.", time],
                TimePunchKind.Out => H["Clocked out at {0}.", time],
                TimePunchKind.MealStart => H["Meal started at {0}.", time],
                TimePunchKind.MealEnd => H["Back from your meal at {0}.", time],
                TimePunchKind.BreakStart => H["Break started at {0}.", time],
                TimePunchKind.BreakEnd => H["Back from your break at {0}.", time],
                _ => H["Punch recorded at {0}.", time],
            });

            if (result.Value.WithinGeofence == false)
            {
                await _notifier.WarningAsync(H["You seem to be away from the work site, so your manager will see this punch."]);
            }
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect(Url.Content("~/my-work"));
    }

    /// <summary>
    /// Shows the worker's timesheet for the pay period a day falls in.
    /// </summary>
    /// <param name="date">The day; today when empty.</param>
    /// <returns>The timesheet.</returns>
    [HttpGet("my-work/timesheet", Name = "MyWorkTimesheet")]
    public async Task<IActionResult> Timesheet(DateOnly? date)
    {
        var (worker, _, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var timesheets = HttpContext.RequestServices.GetRequiredService<ITimesheetService>();
        var result = await timesheets.GetForDateAsync(worker, date ?? await PlanningOptions.TodayAsync(_localClock), HttpContext.RequestAborted);
        var model = new MyTimesheetViewModel { Nav = await _portal.NavAsync(User, worker, "timesheet", HttpContext.RequestAborted) };

        if (!result.Succeeded)
        {
            model.Problem = string.Join(" ", result.Errors);

            return View(model);
        }

        model.Details = await HttpContext.RequestServices.GetRequiredService<TimesheetViews>().BuildAsync(result.Value, worker, HttpContext.RequestAborted);
        model.Details.PreviousPeriod = result.Value.PeriodStart.AddDays(-1);
        model.Details.NextPeriod = result.Value.PeriodEnd.AddDays(1) <= await PlanningOptions.TodayAsync(_localClock) ? result.Value.PeriodEnd.AddDays(1) : null;
        model.Attestation = Attestation;

        return View(model);
    }

    /// <summary>
    /// Submits the worker's timesheet.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="attest">Whether the worker ticked that the hours are right.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost("my-work/timesheet/{id}/submit")]
    public async Task<IActionResult> Submit(string id, bool attest)
    {
        var (worker, _, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var timesheets = HttpContext.RequestServices.GetRequiredService<ITimesheetService>();
        var timesheet = await timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null || timesheet.WorkerId != worker.ItemId)
        {
            return NotFound();
        }

        var result = await timesheets.SubmitAsync(id, attest ? Attestation : null, User.Identity?.Name, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Your timesheet has been sent to your manager."]);
        }

        return Redirect(Url.Content("~/my-work/timesheet?date=" + timesheet.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Asks for a missed punch to be added.
    /// </summary>
    /// <param name="kind">The punch.</param>
    /// <param name="date">The day.</param>
    /// <param name="time">The time.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost("my-work/timesheet/correction")]
    public async Task<IActionResult> Correction(TimePunchKind kind, DateOnly date, TimeOnly time, string reason)
    {
        var (worker, _, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var timesheets = HttpContext.RequestServices.GetRequiredService<ITimesheetService>();
        var timesheet = await timesheets.GetForDateAsync(worker, date, HttpContext.RequestAborted);
        var zone = timesheet.Succeeded ? (await timesheets.CalculateAsync(timesheet.Value.ItemId, HttpContext.RequestAborted)).Calculation?.TimeZoneId : null;
        var result = await HttpContext.RequestServices.GetRequiredService<IAttendanceService>().RequestCorrectionAsync(worker, kind, CalendarTime.ToUtc(date.ToDateTime(time), zone ?? "UTC"), reason, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Your manager has been asked to add the punch."]);
        }

        return Redirect(Url.Content("~/my-work/timesheet?date=" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    private async Task<(WorkerProfile Worker, ITimeClockService TimeClock, IActionResult Denied)> AuthorizeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return (null, null, Challenge());
        }

        var timeClock = HttpContext.RequestServices.GetService<ITimeClockService>();

        if (timeClock is null)
        {
            return (null, null, NotFound());
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal) || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.PunchTime))
        {
            return (null, null, Forbid());
        }

        var worker = await _portal.GetWorkerAsync(User);

        return worker is null ? (null, null, Redirect(Url.Content("~/my-work"))) : (worker, timeClock, null);
    }
}
