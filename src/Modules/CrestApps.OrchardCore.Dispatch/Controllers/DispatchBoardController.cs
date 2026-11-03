using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The dispatch board: a day's technicians on a timeline, the unscheduled visits, and a map.
/// </summary>
public sealed class DispatchBoardController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IDispatchService _dispatch;
    private readonly IDispatchCandidateFinder _finder;
    private readonly DispatchResources _resources;
    private readonly ITeamManager _teams;
    private readonly WorkOrderStore _workOrders;
    private readonly DispatchTimeZone _timeZone;
    private readonly IGeoSettingsService _geoSettings;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchBoardController"/> class.
    /// </summary>
    public DispatchBoardController(
        IAuthorizationService authorizationService,
        IDispatchService dispatch,
        IDispatchCandidateFinder finder,
        DispatchResources resources,
        ITeamManager teams,
        WorkOrderStore workOrders,
        DispatchTimeZone timeZone,
        IGeoSettingsService geoSettings,
        IClock clock,
        IStringLocalizer<DispatchBoardController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _dispatch = dispatch;
        _finder = finder;
        _resources = resources;
        _teams = teams;
        _workOrders = workOrders;
        _timeZone = timeZone;
        _geoSettings = geoSettings;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the board.
    /// </summary>
    [Admin("dispatch/board", "DispatchBoard")]
    public async Task<IActionResult> Index(string day, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ViewDispatch))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var teams = await _teams.ListTreeAsync(HttpContext.RequestAborted);

        return View(new DispatchBoardViewModel
        {
            Day = ParseDay(day, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeZoneId = zone,
            TeamId = teamId,
            TeamOptions = [.. teams.Select(entry => new SelectListItem(new string(' ', entry.Depth * 2) + entry.Team.Name, entry.Team.ItemId, entry.Team.ItemId == teamId))],
            CanManage = await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch),
            HubUrl = HttpContext.RequestServices.GetService(typeof(RealTime.DispatchBoardNotifier)) is null ? null : SignalRHubRoutes.GetTenantAwareHubUrl<RealTime.DispatchHub>(HttpContext),
            Map = await _geoSettings.GetAsync(),
        });
    }

    /// <summary>
    /// Gives the board's data for a day: technicians with their shifts and visits, the unscheduled visits, and places.
    /// </summary>
    [Admin("dispatch/board/data", "DispatchBoardData")]
    public async Task<IActionResult> Data(string day, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ViewDispatch))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var date = ParseDay(day, zone);
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var teamIds = string.IsNullOrEmpty(teamId) ? null : await _teams.GetSubtreeIdsAsync(teamId, HttpContext.RequestAborted);
        var workers = (await _resources.ListWorkersAsync(HttpContext.RequestAborted))
            .Where(worker => teamIds is null || worker.MembershipsOn(date).Any(membership => teamIds.Contains(membership.TeamId)))
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var rows = new List<object>();
        var earliest = 24d;
        var latest = 0d;

        foreach (var worker in workers)
        {
            var availability = await _resources.GetAvailabilityAsync(worker, fromUtc, toUtc, null, HttpContext.RequestAborted);
            var team = await _resources.FindTeamAsync(worker.PrimaryTeamId ?? worker.MembershipsOn(date).Select(membership => membership.TeamId).FirstOrDefault(), HttpContext.RequestAborted);

            foreach (var span in availability.Open)
            {
                earliest = Math.Min(earliest, Hours(span.Start, fromUtc));
                latest = Math.Max(latest, Hours(span.End, fromUtc));
            }

            rows.Add(new
            {
                id = worker.ItemId,
                name = worker.DisplayName,
                team = team?.Name,
                open = availability.Open.Select(span => new { start = span.Start, end = span.End }),
                timeOff = availability.TimeOff.Select(span => new { start = span.Start, end = span.End }),
                onShift = availability.HasShift,
            });
        }

        var scheduled = await _dispatch.ListScheduledAsync(fromUtc, toUtc, teamIds is null ? null : workers.Select(worker => worker.ItemId).ToArray(), HttpContext.RequestAborted);
        var unscheduled = await _dispatch.ListUnscheduledAsync(200, HttpContext.RequestAborted);

        foreach (var visit in scheduled)
        {
            earliest = Math.Min(earliest, Hours(visit.StartUtc.Value.AddMinutes(-(visit.TravelMinutes ?? 0)), fromUtc));
            latest = Math.Max(latest, Hours(visit.EndUtc.Value, fromUtc));
        }

        var visits = new List<object>();

        foreach (var visit in scheduled)
        {
            visits.Add(await ToJsonAsync(visit, zone));
        }

        var queue = new List<object>();

        foreach (var visit in unscheduled)
        {
            queue.Add(await ToJsonAsync(visit, zone));
        }

        // Where technicians are now, from the app, when they share it; only today's board shows them.
        var positions = new List<object>();

        if (HttpContext.RequestServices.GetService<IWorkerLocationService>() is { } locations && _clock.UtcNow >= fromUtc && _clock.UtcNow < toUtc)
        {
            var names = workers.ToDictionary(worker => worker.ItemId, worker => worker.DisplayName, StringComparer.Ordinal);

            foreach (var ping in await locations.LatestAsync(names.Keys, _clock.UtcNow.AddMinutes(-30), HttpContext.RequestAborted))
            {
                positions.Add(new
                {
                    workerId = ping.WorkerId,
                    name = names.GetValueOrDefault(ping.WorkerId),
                    latitude = ping.Latitude,
                    longitude = ping.Longitude,
                    time = DispatchTimeZone.Format(ping.Utc, zone, "HH:mm"),
                });
            }
        }

        return Json(new
        {
            day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            title = date.ToString("dddd d MMMM yyyy", CultureInfo.CurrentCulture),
            zone,
            fromUtc,
            firstHour = earliest > latest ? 7 : Math.Max(0, Math.Floor(earliest) - 1),
            lastHour = earliest > latest ? 19 : Math.Min(24, Math.Ceiling(latest) + 1),
            nowUtc = _clock.UtcNow,
            rows,
            visits,
            unscheduled = queue,
            positions,
        });
    }

    /// <summary>
    /// Moves a visit dropped on a technician's row at a time.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/board/move", "DispatchBoardMove")]
    public async Task<IActionResult> Move([FromBody] BoardMoveModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var visit = await _dispatch.FindAsync(model?.VisitId, HttpContext.RequestAborted);

        if (visit is null)
        {
            return Json(new { succeeded = false, errors = new[] { S["The visit doesn't exist."].Value } });
        }

        // Dropped on another technician: they lead it, the rest of the crew stays.
        var workerIds = new List<string> { model.WorkerId };

        workerIds.AddRange(visit.WorkerIds.Where(id => id != visit.LeadWorkerId && id != model.WorkerId));

        var result = await _dispatch.AssignAsync(visit.ItemId, new VisitAssignment { WorkerIds = workerIds, StartUtc = DateTime.SpecifyKind(model.StartUtc.ToUniversalTime(), DateTimeKind.Utc) }, User.Identity?.Name, HttpContext.RequestAborted);

        return Json(new { succeeded = result.Succeeded, errors = result.Errors });
    }

    /// <summary>
    /// Proposes a better arrangement of the day shown; nothing changes until it is applied.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/board/optimize", "DispatchBoardOptimize")]
    public async Task<IActionResult> Optimize([FromBody] BoardOptimizeModel model, [FromServices] IDispatchOptimizationService optimization)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var date = ParseDay(model?.Day, zone);
        IReadOnlyCollection<string> workerIds = null;

        if (!string.IsNullOrEmpty(model?.TeamId))
        {
            var teamIds = await _teams.GetSubtreeIdsAsync(model.TeamId, HttpContext.RequestAborted);

            workerIds = [.. (await _resources.ListWorkersAsync(HttpContext.RequestAborted)).Where(worker => worker.MembershipsOn(date).Any(membership => teamIds.Contains(membership.TeamId))).Select(worker => worker.ItemId)];
        }

        var proposal = await optimization.ProposeAsync(date, workerIds, model?.IncludeUnscheduled == true, HttpContext.RequestAborted);
        var names = (await _resources.ListWorkersAsync(HttpContext.RequestAborted)).ToDictionary(worker => worker.ItemId, worker => worker.DisplayName, StringComparer.Ordinal);

        return Json(new
        {
            solver = proposal.Solver,
            travelBefore = proposal.TravelBefore,
            travelAfter = proposal.TravelAfter,
            unassigned = proposal.Unassigned.Count,
            moves = proposal.Moves.Select(move => new
            {
                visitId = move.VisitId,
                number = move.Number,
                summary = move.Summary,
                from = move.FromStartUtc is null ? null : $"{names.GetValueOrDefault(move.FromWorkerId ?? string.Empty, move.FromWorkerId)} {DispatchTimeZone.Format(move.FromStartUtc, zone, "HH:mm")}",
                to = $"{names.GetValueOrDefault(move.ToWorkerId, move.ToWorkerId)} {DispatchTimeZone.Format(move.ToStartUtc, zone, "HH:mm")}",
                fromWorkerId = move.FromWorkerId,
                fromStartUtc = move.FromStartUtc,
                toWorkerId = move.ToWorkerId,
                toStartUtc = move.ToStartUtc,
            }),
        });
    }

    /// <summary>
    /// Applies the moves chosen from a proposal.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/board/apply", "DispatchBoardApply")]
    public async Task<IActionResult> Apply([FromBody] List<ProposalMove> moves, [FromServices] IDispatchOptimizationService optimization)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var result = await optimization.ApplyAsync(moves ?? [], User.Identity?.Name, HttpContext.RequestAborted);

        return Json(new { applied = result.Applied.Count, errors = result.Errors.Values });
    }

    /// <summary>
    /// Gives the best technicians for a visit on a day.
    /// </summary>
    [Admin("dispatch/board/candidates/{visitId}", "DispatchBoardCandidates")]
    public async Task<IActionResult> Candidates(string visitId, string day)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var visit = await _dispatch.FindAsync(visitId, HttpContext.RequestAborted);

        if (visit is null)
        {
            return NotFound();
        }

        var zone = await _timeZone.GetAsync();
        var date = ParseDay(day, zone);
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var found = await _finder.FindAsync(visit, fromUtc, fromUtc.AddDays(1), null, HttpContext.RequestAborted);

        return Json(new
        {
            candidates = found.Candidates.Take(8).Select(candidate => new
            {
                workerId = candidate.WorkerId,
                name = candidate.Name,
                startUtc = candidate.StartUtc,
                start = DispatchTimeZone.Format(candidate.StartUtc, zone, "HH:mm"),
                end = DispatchTimeZone.Format(candidate.EndUtc, zone, "HH:mm"),
                travelMinutes = candidate.TravelMinutes,
                score = candidate.Score,
                parts = candidate.ScoreParts,
                warnings = candidate.Warnings,
            }),
            excluded = found.Excluded.Select(exclusion => new { name = exclusion.Name, reason = exclusion.Reason }),
            message = found.Message,
        });
    }

    private async Task<object> ToJsonAsync(ServiceVisit visit, string zone)
    {
        var place = await _resources.PlaceOfAsync(visit.WorkOrderId, HttpContext.RequestAborted);

        return new
        {
            id = visit.ItemId,
            number = visit.Number,
            workOrderNumber = visit.WorkOrderNumber,
            summary = visit.Summary,
            status = visit.Status.ToString(),
            statusText = StatusText(visit.Status),
            priority = visit.Priority.ToString(),
            startUtc = visit.StartUtc,
            endUtc = visit.EndUtc,
            start = DispatchTimeZone.Format(visit.StartUtc, zone, "HH:mm"),
            end = DispatchTimeZone.Format(visit.EndUtc, zone, "HH:mm"),
            due = DispatchTimeZone.Format(visit.DueUtc, zone),
            late = visit.DueUtc is { } due && (visit.EndUtc ?? _clock.UtcNow) > due,
            durationMinutes = visit.DurationMinutes,
            travelMinutes = visit.TravelMinutes,
            workerIds = visit.WorkerIds,
            leadWorkerId = visit.LeadWorkerId,
            pinned = visit.Pinned,
            atRisk = visit.AtRiskReason,
            eta = visit.Status == VisitStatus.EnRoute ? DispatchTimeZone.Format(visit.EtaUtc, zone, "HH:mm") : null,
            canMove = VisitStatusRules.CanAssign(visit.Status) && !visit.Pinned,
            latitude = place?.Latitude,
            longitude = place?.Longitude,
            url = Url.Action(nameof(VisitsController.Details), "Visits", new { area = DispatchConstants.Features.Dispatch, id = visit.ItemId }),
        };
    }

    private string StatusText(VisitStatus status) => status switch
    {
        VisitStatus.Unscheduled => S["Unscheduled"],
        VisitStatus.Scheduled => S["Scheduled"],
        VisitStatus.Dispatched => S["Dispatched"],
        VisitStatus.EnRoute => S["En route"],
        VisitStatus.OnSite => S["On site"],
        VisitStatus.Paused => S["Paused"],
        VisitStatus.Completed => S["Completed"],
        VisitStatus.Incomplete => S["Incomplete"],
        _ => S["Canceled"],
    };

    private DateOnly ParseDay(string day, string zone)
        => DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateOnly.FromDateTime(CalendarTime.ToLocal(_clock.UtcNow, zone));

    private static double Hours(DateTime utc, DateTime fromUtc) => Math.Clamp((utc - fromUtc).TotalHours, 0, 24);
}
