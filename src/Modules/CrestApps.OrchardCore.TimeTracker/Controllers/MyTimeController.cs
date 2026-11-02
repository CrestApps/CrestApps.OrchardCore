using System.Security.Claims;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.TimeTracker.Controllers;

/// <summary>
/// The person's own clock: clock in, switch stage, clock out, and their day and week.
/// </summary>
public sealed class MyTimeController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeTrackerService _tracker;
    private readonly TimeStageStore _stages;
    private readonly TimeCategoryStore _categories;
    private readonly TimeTrackerTimeZone _timeZone;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTimeController"/> class.
    /// </summary>
    public MyTimeController(IAuthorizationService authorizationService, ITimeTrackerService tracker, TimeStageStore stages, TimeCategoryStore categories, TimeTrackerTimeZone timeZone, IClock clock, INotifier notifier, IHtmlLocalizer<MyTimeController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _tracker = tracker;
        _stages = stages;
        _categories = categories;
        _timeZone = timeZone;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the clock, today and this week.
    /// </summary>
    [Admin("time/my", "TimeTrackerMine")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.TrackOwnTime))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var zone = await _timeZone.GetAsync();
        var now = _clock.UtcNow;
        var today = TimeTrackerTimeZone.ToLocal(now, zone).Date;
        var dayStart = TimeTrackerTimeZone.ToUtc(today, zone);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var stages = (await _stages.GetAllAsync(HttpContext.RequestAborted)).OrderBy(stage => stage.Position).ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        return View(new MyTimeViewModel
        {
            Status = await _tracker.GetStatusAsync(userId, HttpContext.RequestAborted),
            Stages = [.. stages.Where(stage => stage.Enabled && stage.SelfService)],
            ExternalClock = await ExternalClockAsync(userId),
            AllStages = stages.ToDictionary(stage => stage.ItemId, StringComparer.Ordinal),
            Today = [.. await _tracker.ListAsync(new TimeEntryQuery { UserIds = [userId], FromUtc = dayStart, ToUtc = dayStart.AddDays(1) }, HttpContext.RequestAborted)],
            Week = [.. await _tracker.GetTotalsAsync(new TimeEntryQuery { UserIds = [userId], FromUtc = TimeTrackerTimeZone.ToUtc(monday, zone), ToUtc = TimeTrackerTimeZone.ToUtc(monday.AddDays(7), zone) }, zone, byDay: true, HttpContext.RequestAborted)],
            Categories = [.. (await _categories.GetAllAsync(HttpContext.RequestAborted)).Where(category => category.Enabled).OrderBy(category => category.Position).ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)],
            TimeZoneId = zone,
            DayStartUtc = dayStart,
            NowUtc = now,
        });
    }

    /// <summary>
    /// Clocks in, or switches to another stage.
    /// </summary>
    [HttpPost]
    [Admin("time/my/switch", "TimeTrackerMineSwitch")]
    public async Task<IActionResult> Switch(string stageId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.TrackOwnTime))
        {
            return Forbid();
        }

        var stage = await _stages.FindByIdAsync(stageId, HttpContext.RequestAborted);

        if (stage is null || !stage.SelfService)
        {
            await _notifier.ErrorAsync(H["Choose one of your stages."]);

            return RedirectToAction(nameof(Index));
        }

        var result = await _tracker.SwitchAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, stageId, TimeTrackerConstants.Sources.Self, User.Identity?.Name, null, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Clocks out.
    /// </summary>
    [HttpPost]
    [Admin("time/my/out", "TimeTrackerMineOut")]
    public async Task<IActionResult> ClockOut()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.TrackOwnTime))
        {
            return Forbid();
        }

        var result = await _tracker.ClockOutAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Clocked out."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ExternalTimeClockInfo> ExternalClockAsync(string userId)
    {
        foreach (var clock in HttpContext.RequestServices.GetServices<IExternalTimeClock>())
        {
            if (await clock.FindAsync(userId, HttpContext.RequestAborted) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
