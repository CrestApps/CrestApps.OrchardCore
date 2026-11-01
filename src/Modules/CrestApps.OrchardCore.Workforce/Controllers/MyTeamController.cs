using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
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
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The My Work portal for managers: approvals waiting for them and who is working now.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
public sealed class MyTeamController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly MyWorkPortal _portal;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IShiftTradeService _tradeService;
    private readonly IOpenShiftService _openShiftService;
    private readonly ICalendarQueryService _queryService;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTeamController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="portal">What the portal's pages share.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="tradeService">The shift trade service.</param>
    /// <param name="openShiftService">The open shift service.</param>
    /// <param name="queryService">The calendar query service.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public MyTeamController(
        IAuthorizationService authorizationService,
        MyWorkPortal portal,
        IWorkforceScopeService scopeService,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IShiftTradeService tradeService,
        IOpenShiftService openShiftService,
        ICalendarQueryService queryService,
        IActivityCodeManager activityCodeManager,
        ISiteService siteService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<MyTeamController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _portal = portal;
        _scopeService = scopeService;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _tradeService = tradeService;
        _openShiftService = openShiftService;
        _queryService = queryService;
        _activityCodeManager = activityCodeManager;
        _siteService = siteService;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the time off, trades and claims waiting for the manager.
    /// </summary>
    /// <returns>The approvals page.</returns>
    [HttpGet("my-work/approvals", Name = "MyWorkApprovals")]
    public async Task<IActionResult> Approvals()
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        IReadOnlyCollection<string> teamIds = scope.All ? null : scope.TeamIds.ToArray();
        var worker = await _portal.GetWorkerAsync(User);
        var model = new MyApprovalsViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "approvals", HttpContext.RequestAborted),
            CanTimeOff = HttpContext.RequestServices.GetService<ITimeOffRequestService>() is not null && await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff),
            CanTrades = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades),
            CanClaims = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts),
        };

        if (model.CanTimeOff)
        {
            var types = (await HttpContext.RequestServices.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
            var (_, requests) = await HttpContext.RequestServices.GetRequiredService<ITimeOffRequestService>().PageAsync(new TimeOffRequestQuery { TeamIds = teamIds }, 0, 50, HttpContext.RequestAborted);

            foreach (var request in requests)
            {
                model.TimeOff.Add(new TimeOffRequestRowViewModel
                {
                    Request = request,
                    WorkerName = (await _workerManager.FindByIdAsync(request.WorkerId))?.DisplayName,
                    TeamName = (await _teamManager.FindByIdAsync(request.TeamId))?.Name,
                    Type = types.TryGetValue(request.TypeId ?? string.Empty, out var type) ? type : null,
                });
            }
        }

        if (model.CanTrades)
        {
            var (_, trades) = await _tradeService.PageAsync(new ShiftTradeQuery { TeamIds = teamIds, Status = ShiftTradeStatus.Accepted }, 0, 50, HttpContext.RequestAborted);

            foreach (var trade in trades)
            {
                var team = await _teamManager.FindByIdAsync(trade.TeamId);
                var zone = await PlanningOptions.TimeZoneOfAsync(team, _siteService);

                model.Trades.Add(new MyTradeViewModel
                {
                    Trade = trade,
                    FromName = (await _workerManager.FindByIdAsync(trade.OfferedByWorkerId))?.DisplayName,
                    ToName = string.IsNullOrEmpty(trade.AcceptedByWorkerId) ? null : (await _workerManager.FindByIdAsync(trade.AcceptedByWorkerId))?.DisplayName,
                    ShiftText = CalendarTime.ToLocal(trade.ShiftStartUtc, zone).ToString("ddd d MMM HH:mm", null),
                    TargetShiftText = trade.TargetShiftStartUtc.HasValue ? CalendarTime.ToLocal(trade.TargetShiftStartUtc.Value, zone).ToString("ddd d MMM HH:mm", null) : null,
                });
            }
        }

        if (model.CanClaims)
        {
            var (_, shifts) = await _openShiftService.PageAsync(new OpenShiftQuery { TeamIds = teamIds, Status = OpenShiftStatus.Claimed }, 0, 50, HttpContext.RequestAborted);

            foreach (var shift in shifts)
            {
                var claims = new List<(OpenShiftClaim, string)>();

                foreach (var claim in shift.Claims.Where(claim => claim.Status == OpenShiftClaimStatus.Pending))
                {
                    claims.Add((claim, (await _workerManager.FindByIdAsync(claim.WorkerId))?.DisplayName));
                }

                model.Claims.Add((shift, (await _teamManager.FindByIdAsync(shift.TeamId))?.Name, claims));
            }
        }

        if (HttpContext.RequestServices.GetService<ITimesheetService>() is { } timesheets && await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            var attendance = HttpContext.RequestServices.GetRequiredService<IAttendanceService>();

            model.CanTimesheets = true;

            foreach (var timesheet in (await timesheets.PageAsync(new TimesheetQuery { TeamIds = teamIds, Statuses = [TimesheetStatus.Submitted] }, 0, 50, HttpContext.RequestAborted)).Items)
            {
                model.Timesheets.Add((timesheet, (await _workerManager.FindByIdAsync(timesheet.WorkerId))?.DisplayName));
            }

            foreach (var correction in await attendance.ListPendingCorrectionsAsync(teamIds, null, HttpContext.RequestAborted))
            {
                var team = await _teamManager.FindByIdAsync(correction.TeamId);
                var zone = await PlanningOptions.TimeZoneOfAsync(team, _siteService);

                model.Corrections.Add((correction, (await _workerManager.FindByIdAsync(correction.WorkerId))?.DisplayName, CalendarTime.ToLocal(correction.Utc, zone).ToString("ddd d MMM HH:mm", null)));
            }

            foreach (var exception in (await attendance.PageAsync(new AttendanceQuery { TeamIds = teamIds, Status = AttendanceExceptionStatus.Open }, 0, 30, HttpContext.RequestAborted)).Items)
            {
                model.Exceptions.Add((exception, (await _workerManager.FindByIdAsync(exception.WorkerId))?.DisplayName));
            }
        }

        return View(model);
    }

    /// <summary>
    /// Approves a submitted timesheet.
    /// </summary>
    /// <param name="id">The timesheet.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/timesheets/{id}")]
    public async Task<IActionResult> ApproveTimesheet(string id)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var timesheets = HttpContext.RequestServices.GetService<ITimesheetService>();

        if (timesheets is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var timesheet = await timesheets.FindAsync(id, HttpContext.RequestAborted);

        if (timesheet is null || !scope.Covers(timesheet.TeamId))
        {
            return NotFound();
        }

        var result = await timesheets.ApproveAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The timesheet has been approved."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Adds or declines the punch a worker asked for.
    /// </summary>
    /// <param name="id">The correction.</param>
    /// <param name="approve">Whether to add it.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/corrections/{id}")]
    public async Task<IActionResult> DecideCorrection(string id, bool approve)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var attendance = HttpContext.RequestServices.GetService<IAttendanceService>();

        if (attendance is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var pending = await attendance.ListPendingCorrectionsAsync(scope.All ? null : scope.TeamIds.ToArray(), null, HttpContext.RequestAborted);

        if (pending.All(correction => correction.ItemId != id))
        {
            return NotFound();
        }

        var result = await attendance.DecideCorrectionAsync(id, approve, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, approve ? H["The punch has been added."] : H["The correction has been declined."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Confirms or excuses an attendance exception.
    /// </summary>
    /// <param name="id">The exception.</param>
    /// <param name="excuse">Whether to excuse it.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/exceptions/{id}")]
    public async Task<IActionResult> DecideException(string id, bool excuse)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var attendance = HttpContext.RequestServices.GetService<IAttendanceService>();

        if (attendance is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimesheets))
        {
            return Forbid();
        }

        var open = await attendance.PageAsync(new AttendanceQuery { TeamIds = scope.All ? null : scope.TeamIds.ToArray(), Status = AttendanceExceptionStatus.Open }, 0, 1000, HttpContext.RequestAborted);

        if (open.Items.All(exception => exception.ItemId != id))
        {
            return NotFound();
        }

        var result = excuse
            ? await attendance.ExcuseAsync(id, null, User.Identity?.Name, HttpContext.RequestAborted)
            : await attendance.ConfirmAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, excuse ? H["The exception has been excused."] : H["The exception has been confirmed."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Approves or declines time off.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <param name="approve">Whether to approve.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/time-off/{id}")]
    public async Task<IActionResult> DecideTimeOff(string id, bool approve)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var requests = HttpContext.RequestServices.GetService<ITimeOffRequestService>();

        if (requests is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveTimeOff))
        {
            return Forbid();
        }

        var request = await requests.FindAsync(id, HttpContext.RequestAborted);

        if (request is null || !scope.Covers(request.TeamId))
        {
            return NotFound();
        }

        var result = approve
            ? await requests.ApproveAsync(id, User.Identity?.Name, null, HttpContext.RequestAborted)
            : await requests.DeclineAsync(id, User.Identity?.Name, null, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, approve ? H["The time off has been approved."] : H["The request has been declined."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Approves or declines an accepted trade.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="approve">Whether to approve.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/trades/{id}")]
    public async Task<IActionResult> DecideTrade(string id, bool approve)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var trade = await _tradeService.FindAsync(id, HttpContext.RequestAborted);

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades) || trade is null || !scope.Covers(trade.TeamId))
        {
            return Forbid();
        }

        var result = approve
            ? await _tradeService.ApproveAsync(id, User.Identity?.Name, HttpContext.RequestAborted)
            : await _tradeService.DeclineAsync(id, null, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, approve ? H["The trade has been approved."] : H["The trade has been declined."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Gives an open shift to a claimant, or declines their claim.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <param name="workerId">The claimant.</param>
    /// <param name="approve">Whether to give them the shift.</param>
    /// <returns>A redirect to the approvals.</returns>
    [HttpPost("my-work/approvals/claims/{id}")]
    public async Task<IActionResult> DecideClaim(string id, string workerId, bool approve)
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var shift = await _openShiftService.FindAsync(id, HttpContext.RequestAborted);

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts) || shift is null || !scope.Covers(shift.TeamId))
        {
            return Forbid();
        }

        var result = approve
            ? await _openShiftService.AssignAsync(id, workerId, User.Identity?.Name, HttpContext.RequestAborted)
            : await _openShiftService.DeclineClaimAsync(id, workerId, null, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, approve ? H["The shift has been given."] : H["The claim has been declined."]);

        return RedirectToAction(nameof(Approvals));
    }

    /// <summary>
    /// Shows who is on shift now, and who is off today, in the teams the manager manages.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("my-work/team", Name = "MyWorkTeam")]
    public async Task<IActionResult> Team()
    {
        var (scope, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var worker = await _portal.GetWorkerAsync(User);
        var now = _clock.UtcNow;
        var codes = (await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).Select(item => item.Team).Where(team => scope.Covers(team.ItemId) && !string.IsNullOrEmpty(team.CalendarId)).ToList();
        var siteZone = await PlanningOptions.TimeZoneOfAsync(null, _siteService);
        var model = new MyTeamNowViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "team", HttpContext.RequestAborted),
            Now = CalendarTime.ToLocal(now, siteZone),
        };

        if (teams.Count > 0)
        {
            var byCalendar = teams.ToDictionary(team => team.CalendarId, StringComparer.Ordinal);
            var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = byCalendar.Keys.ToList(),
                FromUtc = now.AddHours(-16),
                ToUtc = now.AddHours(16),
                EventTypes = [WorkforceConstants.EventTypes.Shift, WorkforceConstants.EventTypes.TimeOff],
                Sources = [WorkforceConstants.Source],
            }, HttpContext.RequestAborted);

            foreach (var occurrence in occurrences.Where(item => item.Event.Status != CalendarEventStatus.Cancelled && item.StartUtc <= now && item.EndUtc > now))
            {
                var team = byCalendar[occurrence.Event.CalendarId];
                var zone = await PlanningOptions.TimeZoneOfAsync(team, _siteService);

                if (occurrence.Event.EventType == WorkforceConstants.EventTypes.TimeOff)
                {
                    model.Off.Add((occurrence.Event.Title, team.Name, occurrence.Event.AllDay ? null : $"{CalendarTime.ToLocal(occurrence.StartUtc, zone):HH:mm}-{CalendarTime.ToLocal(occurrence.EndUtc, zone):HH:mm}"));

                    continue;
                }

                if (!occurrence.Event.TryGet<ShiftData>(out var data))
                {
                    continue;
                }

                var segment = data.Segments.FirstOrDefault(item => item.StartUtc <= now && item.EndUtc > now);
                var doing = segment is null
                    ? null
                    : segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) ? code.Name : segment.Kind.ToString();

                model.Working.Add((occurrence.Event.Title, team.Name, CalendarTime.ToLocal(occurrence.StartUtc, zone), CalendarTime.ToLocal(occurrence.EndUtc, zone), doing));
            }
        }

        model.Working = model.Working.OrderBy(item => item.TeamName).ThenBy(item => item.Name).ToList();

        return View(model);
    }

    private async Task<(WorkforceScope Scope, IActionResult Denied)> AuthorizeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return (null, Challenge());
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return (null, Forbid());
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return scope.All || scope.TeamIds.Count > 0 ? (scope, null) : (null, Forbid());
    }

    private async Task NotifyAsync(IReadOnlyList<string> errors, LocalizedHtmlString success)
    {
        if (errors.Count == 0)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }
}
