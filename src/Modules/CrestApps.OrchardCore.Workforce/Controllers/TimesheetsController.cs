using System.Globalization;
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
/// The timesheets of the teams the user manages: the list, a timesheet in full, its approval, and corrections to its
/// punches and lines.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimesheetsController : Controller
{
    private const int MaxPrepared = 500;

    private readonly IAuthorizationService _authorizationService;
    private readonly ITimesheetService _timesheets;
    private readonly ITimeClockService _timeClock;
    private readonly IAttendanceService _attendance;
    private readonly IPayPeriodCalendarManager _calendars;
    private readonly IPayCodeManager _payCodes;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly TimesheetViews _views;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimesheetsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="timesheets">The timesheets.</param>
    /// <param name="timeClock">The time clock.</param>
    /// <param name="attendance">The attendance service.</param>
    /// <param name="calendars">The pay periods.</param>
    /// <param name="payCodes">The pay codes.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="views">The timesheet page builder.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimesheetsController(
        IAuthorizationService authorizationService,
        ITimesheetService timesheets,
        ITimeClockService timeClock,
        IAttendanceService attendance,
        IPayPeriodCalendarManager calendars,
        IPayCodeManager payCodes,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        TimesheetViews views,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<TimesheetsController> htmlLocalizer,
        IStringLocalizer<TimesheetsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _timesheets = timesheets;
        _timeClock = timeClock;
        _attendance = attendance;
        _calendars = calendars;
        _payCodes = payCodes;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _views = views;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the timesheets of a pay period.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/timesheets", "WorkforceTimesheets")]
    public async Task<IActionResult> Index(TimesheetListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var calendars = await _calendars.ListEnabledAsync(HttpContext.RequestAborted);
        var calendar = calendars.FirstOrDefault(item => item.ItemId == model.CalendarId) ?? (calendars.Count > 0 ? calendars[0] : null);
        var today = await PlanningOptions.TodayAsync(_localClock);

        model.CalendarId = calendar?.ItemId;
        model.CalendarOptions = calendars.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.CalendarId)).ToList();

        if (calendar is not null)
        {
            var current = calendar.PeriodOn(model.PeriodStart ?? today);

            model.PeriodStart = current.Start;
            model.PeriodOptions = PeriodOptions(calendar, today, current.Start);
        }

        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        IReadOnlyCollection<TimesheetStatus> statuses = Enum.TryParse<TimesheetStatus>(model.Status, out var status) ? [status] : [];
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = calendar is null
            ? (0, [])
            : await _timesheets.PageAsync(new TimesheetQuery { TeamIds = teamIds, Statuses = statuses, PayPeriodCalendarId = calendar.ItemId, PeriodStart = model.PeriodStart }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.ApprovedInPeriod = calendar is null
            ? 0
            : (await _timesheets.PageAsync(new TimesheetQuery { TeamIds = teamIds, Statuses = [TimesheetStatus.Approved], PayPeriodCalendarId = calendar.ItemId, PeriodStart = model.PeriodStart }, 0, 0, HttpContext.RequestAborted)).Count;
        model.List = new AdminListContext<TimesheetRowViewModel>
        {
            Source = await RowsAsync(items),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary
            {
                { "teamId", model.TeamId },
                { "status", model.Status },
                { "calendarId", model.CalendarId },
                { "periodStart", model.PeriodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["All"], string.Empty, statuses.Count == 0),
            new SelectListItem(S["Open"], nameof(TimesheetStatus.Open), status == TimesheetStatus.Open && statuses.Count > 0),
            new SelectListItem(S["Submitted"], nameof(TimesheetStatus.Submitted), status == TimesheetStatus.Submitted),
            new SelectListItem(S["Sent back"], nameof(TimesheetStatus.Rejected), status == TimesheetStatus.Rejected),
            new SelectListItem(S["Approved"], nameof(TimesheetStatus.Approved), status == TimesheetStatus.Approved),
            new SelectListItem(S["Reopened"], nameof(TimesheetStatus.Reopened), status == TimesheetStatus.Reopened),
            new SelectListItem(S["Locked"], nameof(TimesheetStatus.Locked), status == TimesheetStatus.Locked),
        ];
        model.BulkActions = [new SelectListItem(S["Approve"], "Approve")];
        model.PeriodEnded = calendar is not null && model.PeriodStart.HasValue && calendar.PeriodOn(model.PeriodStart.Value).End < await PlanningOptions.TodayAsync(_localClock);

        return View(model);
    }

    /// <summary>
    /// Approves the timesheets ticked in the list.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="bulkAction">The action.</param>
    /// <param name="itemIds">The timesheets.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("workforce/timesheets", "WorkforceTimesheets")]
    public async Task<IActionResult> IndexPost(TimesheetListViewModel model, [FromForm(Name = "Options.BulkAction")] string bulkAction, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var done = 0;
        var failed = new List<string>();

        foreach (var id in itemIds ?? [])
        {
            if (bulkAction != "Approve" || !await CanManageAsync(await _timesheets.FindAsync(id, HttpContext.RequestAborted)))
            {
                continue;
            }

            var result = await _timesheets.ApproveAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                done++;
            }
            else
            {
                failed.AddRange(result.Errors);
            }
        }

        if (done > 0)
        {
            await _notifier.SuccessAsync(H.Plural(done, "1 timesheet has been approved.", "{0} timesheets have been approved.", done));
        }

        foreach (var error in failed.Distinct(StringComparer.Ordinal))
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        return RedirectToIndex(model.CalendarId, model.PeriodStart, model.TeamId, model.Status);
    }

    /// <summary>
    /// Makes and works out the period's timesheets of the workers of the teams the user manages that have a time clock
    /// policy.
    /// </summary>
    /// <param name="calendarId">The pay periods.</param>
    /// <param name="periodStart">The period.</param>
    /// <param name="teamId">The team filter.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/prepare", "WorkforceTimesheetsPrepare")]
    public async Task<IActionResult> Prepare(string calendarId, DateOnly periodStart, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(entry => entry.Team)
            .Where(team => scope.Covers(team.ItemId) && (string.IsNullOrEmpty(teamId) || team.ItemId == teamId))
            .ToArray();
        var made = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var team in teams)
        {
            foreach (var worker in await _workerManager.ListByTeamAsync(team.ItemId, periodStart, HttpContext.RequestAborted))
            {
                if (made >= MaxPrepared || !seen.Add(worker.ItemId))
                {
                    continue;
                }

                var result = await _timesheets.GetForDateAsync(worker, periodStart, HttpContext.RequestAborted);

                if (result.Succeeded && result.Value.PayPeriodCalendarId == calendarId)
                {
                    made++;
                }
            }
        }

        await _notifier.SuccessAsync(made == 0
            ? H["No workers of these teams have a time clock policy with these pay periods."]
            : H.Plural(made, "1 timesheet is ready.", "{0} timesheets are ready.", made));

        return RedirectToIndex(calendarId, periodStart, teamId, null);
    }

    /// <summary>
    /// Locks the period's approved timesheets, ready for payroll.
    /// </summary>
    /// <param name="calendarId">The pay periods.</param>
    /// <param name="periodStart">The period.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/lock", "WorkforceTimesheetsLock")]
    public async Task<IActionResult> Lock(string calendarId, DateOnly periodStart)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var locked = await _timesheets.LockPeriodAsync(calendarId, periodStart, User.Identity?.Name, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(locked.Count == 0
            ? H["No approved timesheets to lock in this period. A period is locked once it has ended."]
            : H.Plural(locked.Count, "1 timesheet is locked and ready for payroll.", "{0} timesheets are locked and ready for payroll.", locked.Count));

        return RedirectToIndex(calendarId, periodStart, null, null);
    }

    /// <summary>
    /// Shows a timesheet in full.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/timesheets/{id}", "WorkforceTimesheetsDetails")]
    public async Task<IActionResult> Details(string id)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(timesheet))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(timesheet.WorkerId);

        if (worker is null)
        {
            return NotFound();
        }

        var model = await _views.BuildAsync(timesheet, worker, HttpContext.RequestAborted);

        model.CanApprove = true;
        model.CanEditPunches = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.EditPunches);

        return View(model);
    }

    /// <summary>
    /// Approves, sends back or reopens a timesheet.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="decision">Approve, Reject or Reopen.</param>
    /// <param name="reason">What to fix, when sending it back.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/decide", "WorkforceTimesheetsDecide")]
    public async Task<IActionResult> Decide(string id, string decision, string reason)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(timesheet))
        {
            return Forbid();
        }

        var actor = User.Identity?.Name;
        var result = decision switch
        {
            "Reject" => await _timesheets.RejectAsync(id, reason, actor, HttpContext.RequestAborted),
            "Reopen" => await _timesheets.ReopenAsync(id, actor, HttpContext.RequestAborted),
            _ => await _timesheets.ApproveAsync(id, actor, HttpContext.RequestAborted),
        };

        await ReportAsync(result.Errors, decision switch
        {
            "Reject" => H["The timesheet has been sent back."],
            "Reopen" => H["The timesheet has been reopened."],
            _ => H["The timesheet has been approved."],
        });

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Adds a punch to a timesheet's worker.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="kind">The punch.</param>
    /// <param name="date">The day.</param>
    /// <param name="time">The time, in the worker's time zone.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/punches/add", "WorkforceTimesheetsAddPunch")]
    public async Task<IActionResult> AddPunch(string id, TimePunchKind kind, DateOnly date, TimeOnly time, string reason)
    {
        var (timesheet, zone, denied) = await ForPunchesAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(reason))
        {
            errors.Add(S["Say why the punch is added."]);
        }
        else
        {
            var result = await _timeClock.PunchAsync(new TimePunchRequest
            {
                WorkerId = timesheet.WorkerId,
                Kind = kind,
                Utc = CalendarTime.ToUtc(date.ToDateTime(time), zone),
                Source = TimePunchSource.Manager,
                Reason = reason,
            }, User.Identity?.Name, HttpContext.RequestAborted);

            errors.AddRange(result.Errors);
        }

        await ReportAsync(errors, H["The punch has been added."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Changes a punch.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="punchId">The punch.</param>
    /// <param name="kind">The punch's kind.</param>
    /// <param name="date">The day.</param>
    /// <param name="time">The time, in the worker's time zone.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/punches/correct", "WorkforceTimesheetsCorrectPunch")]
    public async Task<IActionResult> CorrectPunch(string id, string punchId, TimePunchKind kind, DateOnly date, TimeOnly time, string reason)
    {
        var (timesheet, zone, denied) = await ForPunchesAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var punch = await _timeClock.FindAsync(punchId, HttpContext.RequestAborted);

        if (punch is null || punch.WorkerId != timesheet.WorkerId)
        {
            return NotFound();
        }

        var result = await _timeClock.CorrectAsync(punchId, kind, CalendarTime.ToUtc(date.ToDateTime(time), zone), reason, User.Identity?.Name, HttpContext.RequestAborted);

        await ReportAsync(result.Errors, H["The punch has been changed."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Removes a punch.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="punchId">The punch.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/punches/remove", "WorkforceTimesheetsRemovePunch")]
    public async Task<IActionResult> RemovePunch(string id, string punchId, string reason)
    {
        var (timesheet, _, denied) = await ForPunchesAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var punch = await _timeClock.FindAsync(punchId, HttpContext.RequestAborted);

        if (punch is null || punch.WorkerId != timesheet.WorkerId)
        {
            return NotFound();
        }

        var result = await _timeClock.RemoveAsync(punchId, reason, User.Identity?.Name, HttpContext.RequestAborted);

        await ReportAsync(result.Errors, H["The punch has been removed."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Approves or declines a worker's punch correction.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="correctionId">The correction.</param>
    /// <param name="approve">Whether to approve.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/corrections", "WorkforceTimesheetsCorrection")]
    public async Task<IActionResult> DecideCorrection(string id, string correctionId, bool approve)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(timesheet))
        {
            return Forbid();
        }

        var pending = await _attendance.ListPendingCorrectionsAsync(null, timesheet.WorkerId, HttpContext.RequestAborted);

        if (pending.All(correction => correction.ItemId != correctionId))
        {
            return NotFound();
        }

        var result = await _attendance.DecideCorrectionAsync(correctionId, approve, User.Identity?.Name, HttpContext.RequestAborted);

        await ReportAsync(result.Errors, approve ? H["The punch has been added."] : H["The correction has been declined."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Adds a line by hand.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="date">The day.</param>
    /// <param name="payCodeId">The pay code.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="note">A note.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/lines/add", "WorkforceTimesheetsAddLine")]
    public async Task<IActionResult> AddLine(string id, DateOnly date, string payCodeId, decimal hours, string note)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(timesheet))
        {
            return Forbid();
        }

        var result = await _timesheets.AddLineAsync(id, new TimesheetLine { Date = date, PayCodeId = payCodeId, Hours = hours, Note = note }, User.Identity?.Name, HttpContext.RequestAborted);

        await ReportAsync(result.Errors, H["The line has been added."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Removes a line added by hand.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <param name="lineId">The line.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [HttpPost]
    [Admin("workforce/timesheets/{id}/lines/remove", "WorkforceTimesheetsRemoveLine")]
    public async Task<IActionResult> RemoveLine(string id, string lineId)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(timesheet))
        {
            return Forbid();
        }

        var result = await _timesheets.RemoveLineAsync(id, lineId, User.Identity?.Name, HttpContext.RequestAborted);

        await ReportAsync(result.Errors, H["The line has been removed."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Opens a worker's timesheet for the pay period a day falls in.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day; today when empty.</param>
    /// <returns>A redirect to the timesheet.</returns>
    [Admin("workforce/timesheets/worker/{workerId}", "WorkforceTimesheetsWorker")]
    public async Task<IActionResult> Worker(string workerId, DateOnly? date)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _timesheets.GetForDateAsync(worker, date ?? await PlanningOptions.TodayAsync(_localClock), HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await ReportAsync(result.Errors, null);

            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Details), new { id = result.Value.ItemId });
    }

    internal static IList<SelectListItem> PeriodOptions(PayPeriodCalendar calendar, DateOnly today, DateOnly selected)
    {
        var options = new List<SelectListItem>();
        var start = calendar.PeriodOn(today).Start;

        // Back about a year, and the next period.
        for (var index = 0; index < 27; index++)
        {
            start = calendar.PeriodOn(start.AddDays(-1)).Start;
        }

        var last = calendar.PeriodOn(calendar.PeriodOn(today).End.AddDays(1)).Start;

        for (var period = calendar.PeriodOn(start); period.Start <= last; period = calendar.PeriodOn(period.End.AddDays(1)))
        {
            options.Insert(0, new SelectListItem(
                period.Start.ToString("d MMM", CultureInfo.CurrentCulture) + " – " + period.End.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
                period.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                period.Start == selected));
        }

        return options;
    }

    private async Task<(Timesheet Timesheet, string Zone, IActionResult Denied)> ForPunchesAsync(string id)
    {
        var timesheet = await _timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null)
        {
            return (null, null, NotFound());
        }

        if (!await CanManageAsync(timesheet) || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.EditPunches))
        {
            return (null, null, Forbid());
        }

        var (_, calculation) = await _timesheets.CalculateAsync(id, HttpContext.RequestAborted);

        return (timesheet, calculation?.TimeZoneId ?? "UTC", null);
    }

    private async Task<bool> CanManageAsync(Timesheet timesheet)
    {
        if (timesheet is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return false;
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return scope.Covers(timesheet.TeamId);
    }

    private async Task<IList<TimesheetRowViewModel>> RowsAsync(IReadOnlyList<Timesheet> timesheets)
    {
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

        foreach (var workerId in timesheets.Select(timesheet => timesheet.WorkerId).Distinct(StringComparer.Ordinal))
        {
            if (await _workerManager.FindByIdAsync(workerId) is { } worker)
            {
                workers[workerId] = worker;
            }
        }

        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var codes = (await _payCodes.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);

        return timesheets
            .Select(timesheet => new TimesheetRowViewModel
            {
                Timesheet = timesheet,
                Worker = workers.GetValueOrDefault(timesheet.WorkerId),
                TeamName = teams.GetValueOrDefault(timesheet.TeamId ?? string.Empty),
                PayCodes = codes,
            })
            .OrderBy(row => row.Worker?.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private RedirectToActionResult RedirectToIndex(string calendarId, DateOnly? periodStart, string teamId, string status)
    {
        return RedirectToAction(nameof(Index), new { calendarId, periodStart = periodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), teamId, status });
    }

    private async Task ReportAsync(IEnumerable<string> errors, LocalizedHtmlString success)
    {
        var failed = false;

        foreach (var error in errors)
        {
            failed = true;
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        if (!failed && success is not null)
        {
            await _notifier.SuccessAsync(success);
        }
    }
}
