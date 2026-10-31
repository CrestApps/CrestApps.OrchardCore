using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
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
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The team schedules: the list, the planner board, the shift editor, filling from rotations, copying, publishing and
/// the change log.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class SchedulesController : Controller
{
    private const int CoverageIntervalMinutes = 30;

    private readonly IAuthorizationService _authorizationService;
    private readonly ISchedulePlanningService _planningService;
    private readonly ICoverageService _coverageService;
    private readonly IShiftTemplateManager _templateManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="planningService">The schedule planning service.</param>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="templateManager">The shift template manager.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulesController(
        IAuthorizationService authorizationService,
        ISchedulePlanningService planningService,
        ICoverageService coverageService,
        IShiftTemplateManager templateManager,
        IActivityCodeManager activityCodeManager,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IWorkforceScopeService scopeService,
        ISiteService siteService,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<SchedulesController> htmlLocalizer,
        IStringLocalizer<SchedulesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _planningService = planningService;
        _coverageService = coverageService;
        _templateManager = templateManager;
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _scopeService = scopeService;
        _siteService = siteService;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the schedules of the teams the user manages, newest first.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/schedules", "WorkforceSchedules")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, periods) = teamIds is { Count: 0 }
            ? (0, [])
            : await _planningService.ListPeriodsAsync(teamIds, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<SchedulePeriod>
        {
            Source = periods.ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);

        return View(model);
    }

    /// <summary>
    /// Shows the form that starts a schedule.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <returns>The form.</returns>
    [Admin("workforce/schedules/create", "WorkforceSchedulesCreate")]
    public async Task<IActionResult> Create(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var model = new ScheduleCreateViewModel
        {
            TeamId = teamId,
            StartDate = await PlanningOptions.NextMondayAsync(_localClock),
        };

        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);

        return View(model);
    }

    /// <summary>
    /// Starts a schedule.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the planner board, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/schedules/create", "WorkforceSchedulesCreate")]
    public async Task<IActionResult> CreatePost(ScheduleCreateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!string.IsNullOrEmpty(model.TeamId) && !scope.Covers(model.TeamId))
        {
            return Forbid();
        }

        if (model.StartDate is null)
        {
            ModelState.AddModelError(nameof(model.StartDate), S["Choose the first day."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _planningService.CreatePeriodAsync(model.TeamId, model.StartDate.Value, model.Weeks, model.Name, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Plan), new { id = result.Value.ItemId });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);

        return View(model);
    }

    /// <summary>
    /// Shows the planner board of a schedule, one week at a time.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <param name="week">The week to show, from 0.</param>
    /// <returns>The board.</returns>
    [Admin("workforce/schedules/plan/{id}", "WorkforceSchedulesPlan")]
    public async Task<IActionResult> Plan(string id, int week = 0)
    {
        var (period, team, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var weekCount = ((period.EndDate.DayNumber - period.StartDate.DayNumber) / 7) + 1;

        week = Math.Clamp(week, 0, weekCount - 1);

        var first = period.StartDate.AddDays(week * 7);
        var days = Enumerable.Range(0, 7).Select(first.AddDays).Where(day => day <= period.EndDate).ToList();
        var drafts = await _planningService.ListDraftShiftsAsync(period.PrimaryScenarioId, HttpContext.RequestAborted);
        var templates = (await _templateManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(template => template.ItemId, StringComparer.Ordinal);
        var codes = (await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var model = new ScheduleBoardViewModel
        {
            Period = period,
            TeamName = team.Name,
            CalendarId = team.CalendarId,
            Week = week,
            WeekCount = weekCount,
            Days = days,
            ShiftCount = drafts.Count,
            OpenCount = drafts.Count(draft => draft.WorkerId is null),
            CanPublish = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.PublishSchedules),
            LastRun = (await _planningService.FindScenarioAsync(period.PrimaryScenarioId, HttpContext.RequestAborted))?.LastRun,
        };

        var members = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

        foreach (var day in days)
        {
            foreach (var worker in await _workerManager.ListByTeamAsync(team.ItemId, day, HttpContext.RequestAborted))
            {
                members.TryAdd(worker.ItemId, worker);
            }
        }

        var weekDrafts = drafts.Where(draft => days.Contains(draft.Date)).ToList();
        var rows = members.Values.ToDictionary(
            worker => worker.ItemId,
            worker => new ScheduleBoardRowViewModel { WorkerId = worker.ItemId, Name = worker.DisplayName, OnTeam = true },
            StringComparer.Ordinal);

        foreach (var workerId in weekDrafts.Select(draft => draft.WorkerId).Where(workerId => workerId is not null && !rows.ContainsKey(workerId)).Distinct(StringComparer.Ordinal).ToList())
        {
            var worker = await _workerManager.FindByIdAsync(workerId);

            rows[workerId] = new ScheduleBoardRowViewModel { WorkerId = workerId, Name = worker?.DisplayName ?? workerId, OnTeam = false };
        }

        var open = new ScheduleBoardRowViewModel { Name = S["Open shifts"] };

        foreach (var draft in weekDrafts)
        {
            var row = draft.WorkerId is null ? open : rows[draft.WorkerId];
            var template = draft.TemplateId is null ? null : templates.GetValueOrDefault(draft.TemplateId);

            if (!row.Cells.TryGetValue(draft.Date, out var cell))
            {
                row.Cells[draft.Date] = cell = [];
            }

            cell.Add(new ScheduleBoardShiftViewModel
            {
                Id = draft.ItemId,
                Start = draft.Start,
                End = draft.End,
                TemplateName = template?.Name,
                Color = template?.Color,
                Segments = string.Join(", ", draft.Segments.Select(segment => $"{codes.GetValueOrDefault(segment.ActivityCodeId ?? string.Empty)?.Name} {segment.Start:HH\\:mm}")),
            });

            var unpaid = draft.Segments
                .Where(segment => segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) && !code.IsPaid)
                .Sum(segment => (segment.End - segment.Start).TotalMinutes);

            row.Hours += (decimal)Math.Round(((draft.End - draft.Start).TotalMinutes - unpaid) / 60, 2);
        }

        model.Rows = rows.Values
            .OrderBy(row => !row.OnTeam)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Append(open)
            .ToList();

        await AddCoverageAsync(model, period, team);

        model.CopySourceOptions = (await _planningService.ListPeriodsAsync([team.ItemId], 0, 50, HttpContext.RequestAborted)).Items
            .Where(other => other.ItemId != period.ItemId)
            .Select(other => new SelectListItem($"{other.Name} ({other.StartDate.ToString("d", CultureInfo.CurrentCulture)})", other.ItemId))
            .ToList();

        return View(model);
    }

    /// <summary>
    /// Shows the editor of a new or existing draft shift.
    /// </summary>
    /// <param name="periodId">The schedule.</param>
    /// <param name="id">The draft shift id, or empty for a new shift.</param>
    /// <param name="workerId">The worker of a new shift.</param>
    /// <param name="date">The day of a new shift.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/schedules/shift/{periodId}/{id?}", "WorkforceSchedulesShift")]
    public async Task<IActionResult> Shift(string periodId, string id, string workerId, DateOnly? date)
    {
        var (period, team, denied) = await AuthorizePeriodAsync(periodId);

        if (denied is not null)
        {
            return denied;
        }

        var codes = await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted);
        DraftShiftEditViewModel model;

        if (string.IsNullOrEmpty(id))
        {
            model = new DraftShiftEditViewModel
            {
                PeriodId = period.ItemId,
                WorkerId = workerId,
                Date = date ?? period.StartDate,
                Start = "09:00",
                End = "17:00",
            };
        }
        else
        {
            var shift = await FindDraftAsync(period, id);

            if (shift is null)
            {
                return NotFound();
            }

            model = new DraftShiftEditViewModel
            {
                Id = shift.ItemId,
                PeriodId = period.ItemId,
                WorkerId = shift.WorkerId,
                Date = shift.Date,
                TemplateId = shift.TemplateId,
                Start = shift.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                End = shift.End.ToString("HH:mm", CultureInfo.InvariantCulture),
                Segments = SegmentFormat.Write(SegmentFormat.FromDraft(shift), codes, TimeOnly.FromDateTime(shift.Start)),
                Notes = shift.Notes,
            };
        }

        await PopulateShiftAsync(model, period, team, codes);

        return View(model);
    }

    /// <summary>
    /// Saves a draft shift.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <param name="applyTemplate">Whether to take the times and breaks from the chosen template.</param>
    /// <returns>A redirect to the planner board, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Shift))]
    [Admin("workforce/schedules/shift/{periodId}/{id?}", "WorkforceSchedulesShift")]
    public async Task<IActionResult> ShiftPost(DraftShiftEditViewModel model, bool applyTemplate = false)
    {
        var (period, team, denied) = await AuthorizePeriodAsync(model.PeriodId);

        if (denied is not null)
        {
            return denied;
        }

        var codes = await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted);
        var template = string.IsNullOrEmpty(model.TemplateId) ? null : await _templateManager.FindByIdAsync(model.TemplateId);

        if (applyTemplate)
        {
            ModelState.Clear();

            if (template is not null)
            {
                model.Start = template.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
                model.End = template.Start.AddMinutes(template.LengthMinutes).ToString("HH:mm", CultureInfo.InvariantCulture);
                model.Segments = SegmentFormat.Write(SegmentFormat.FromTemplate(template), codes, template.Start);
            }

            await PopulateShiftAsync(model, period, team, codes);

            return View(model);
        }

        var shift = string.IsNullOrEmpty(model.Id)
            ? new DraftShift { ScenarioId = period.PrimaryScenarioId }
            : await FindDraftAsync(period, model.Id);

        if (shift is null)
        {
            return NotFound();
        }

        if (model.Date is null)
        {
            ModelState.AddModelError(nameof(model.Date), S["Choose the day."]);
        }

        if (!PlanningInput.TryParseTime(model.Start, out var start))
        {
            ModelState.AddModelError(nameof(model.Start), S["Enter when the shift starts, such as 09:00."]);
        }

        if (!PlanningInput.TryParseTime(model.End, out var end))
        {
            ModelState.AddModelError(nameof(model.End), S["Enter when the shift ends, such as 17:00."]);
        }

        var errors = new List<string>();
        var segments = SegmentFormat.Read(model.Segments, codes, errors);

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(model.Segments), S["This break or activity isn't understood: {0}", error]);
        }

        if (ModelState.IsValid)
        {
            var (from, to) = PlanningInput.OnDay(model.Date.Value, start, end);

            shift.WorkerId = string.IsNullOrEmpty(model.WorkerId) ? null : model.WorkerId;
            shift.Date = model.Date.Value;
            shift.Start = from;
            shift.End = to;
            shift.TemplateId = template?.ItemId;
            shift.Segments = SegmentFormat.ToDraft(segments, from);
            shift.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            var result = await _planningService.SaveDraftShiftAsync(shift, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The shift has been saved."]);

                return RedirectToAction(nameof(Plan), new { id = period.ItemId, week = WeekOf(period, shift.Date) });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        await PopulateShiftAsync(model, period, team, codes);

        return View(model);
    }

    /// <summary>
    /// Deletes a draft shift.
    /// </summary>
    /// <param name="periodId">The schedule.</param>
    /// <param name="id">The draft shift id.</param>
    /// <returns>A redirect to the planner board.</returns>
    [HttpPost]
    [Admin("workforce/schedules/delete-shift/{periodId}/{id}", "WorkforceSchedulesDeleteShift")]
    public async Task<IActionResult> DeleteShift(string periodId, string id)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(periodId);

        if (denied is not null)
        {
            return denied;
        }

        var shift = await FindDraftAsync(period, id);

        if (shift is not null && await _planningService.DeleteDraftShiftAsync(id, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The shift has been deleted."]);
        }

        return RedirectToAction(nameof(Plan), new { id = period.ItemId, week = shift is null ? 0 : WeekOf(period, shift.Date) });
    }

    /// <summary>
    /// Adds the shifts of the team's rotations to a schedule.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the planner board.</returns>
    [HttpPost]
    [Admin("workforce/schedules/apply-rotations/{id}", "WorkforceSchedulesApplyRotations")]
    public async Task<IActionResult> ApplyRotations(string id)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var added = await _planningService.ApplyPatternsAsync(period.PrimaryScenarioId, User.Identity?.Name, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} shifts were added from the rotations.", added]);

        return RedirectToAction(nameof(Plan), new { id });
    }

    /// <summary>
    /// Adds shifts to a schedule with the schedule solver, to cover the team's coverage requirements.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the planner board.</returns>
    [HttpPost]
    [Admin("workforce/schedules/auto-schedule/{id}", "WorkforceSchedulesAutoSchedule")]
    public async Task<IActionResult> AutoSchedule(string id)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _planningService.AutoScheduleAsync(period.PrimaryScenarioId, new AutoScheduleOptions(), User.Identity?.Name, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.WarningAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H.Plural(result.Run.ShiftsAdded, "1 shift was added. The schedule now covers {1:P0} of the people wanted and {2:P0} of the fewest needed.", "{0} shifts were added. The schedule now covers {1:P0} of the people wanted and {2:P0} of the fewest needed.", result.Run.ShiftsAdded, result.Run.CoverageScore, result.Run.MinimumScore));

            if (result.Run.TimedOut)
            {
                await _notifier.InformationAsync(H["The scheduler stopped at its time limit; running it again may add more."]);
            }
        }

        return RedirectToAction(nameof(Plan), new { id });
    }

    /// <summary>
    /// Copies the shifts of another schedule of the team into a schedule.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <param name="sourceId">The schedule to copy from.</param>
    /// <returns>A redirect to the planner board.</returns>
    [HttpPost]
    [Admin("workforce/schedules/copy/{id}", "WorkforceSchedulesCopy")]
    public async Task<IActionResult> Copy(string id, string sourceId)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var source = await _planningService.FindPeriodAsync(sourceId, HttpContext.RequestAborted);

        if (source is null || source.TeamId != period.TeamId)
        {
            await _notifier.WarningAsync(H["Choose another schedule of the same team to copy from."]);

            return RedirectToAction(nameof(Plan), new { id });
        }

        var copied = await _planningService.CopyFromPeriodAsync(period.PrimaryScenarioId, source.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} shifts were copied from {1}.", copied, source.Name]);

        return RedirectToAction(nameof(Plan), new { id });
    }

    /// <summary>
    /// Publishes a schedule to the team's calendar.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the planner board.</returns>
    [HttpPost]
    [Admin("workforce/schedules/publish/{id}", "WorkforceSchedulesPublish")]
    public async Task<IActionResult> Publish(string id)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(id, WorkforcePermissions.PublishSchedules);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _planningService.PublishAsync(period.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        foreach (var error in result.Errors)
        {
            await _notifier.WarningAsync(H["{0}", error]);
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The schedule has been published: {0} shifts added, {1} changed and {2} removed.", result.Added, result.Updated, result.Removed]);
        }
        else
        {
            await _notifier.WarningAsync(H["The schedule was published with problems: {0} shifts added, {1} changed and {2} removed; {3} could not be published.", result.Added, result.Updated, result.Removed, result.Errors.Count]);
        }

        if (result.Open > 0)
        {
            await _notifier.InformationAsync(H.Plural(result.Open, "1 shift has nobody: it stays off the calendar and is offered under Open Shifts.", "{0} shifts have nobody: they stay off the calendar and are offered under Open Shifts.", result.Open));
        }

        return RedirectToAction(nameof(Plan), new { id });
    }

    /// <summary>
    /// Lists the changes made to a schedule after it was first published.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The change log.</returns>
    [Admin("workforce/schedules/changes/{id}", "WorkforceSchedulesChanges")]
    public async Task<IActionResult> Changes(string id, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var (period, team, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var timeZoneId = await TimeZoneOfAsync(team);
        var changes = (await _planningService.ListChangesAsync(period.ItemId, HttpContext.RequestAborted)).Reverse().ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var page = changes.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var workerId in page.SelectMany(change => new[] { change.WorkerId, change.PreviousWorkerId }).Where(workerId => workerId is not null).Distinct(StringComparer.Ordinal))
        {
            names[workerId] = (await _workerManager.FindByIdAsync(workerId))?.DisplayName ?? workerId;
        }

        ViewData["Period"] = period;

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<ScheduleChangeRowViewModel>
            {
                Source = page.Select(change => new ScheduleChangeRowViewModel
                {
                    Change = change,
                    WorkerName = change.WorkerId is null ? null : names.GetValueOrDefault(change.WorkerId),
                    PreviousWorkerName = change.PreviousWorkerId is null ? null : names.GetValueOrDefault(change.PreviousWorkerId),
                    TimeZoneId = timeZoneId,
                }).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, changes.Count),
            },
        });
    }

    /// <summary>
    /// Deletes a schedule that was never published.
    /// </summary>
    /// <param name="id">The schedule id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/schedules/delete/{id}", "WorkforceSchedulesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var (period, _, denied) = await AuthorizePeriodAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (await _planningService.DeletePeriodAsync(period.ItemId, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The schedule has been deleted."]);
        }
        else
        {
            await _notifier.WarningAsync(H["A published schedule can't be deleted; remove its shifts and publish again instead."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<(SchedulePeriod Period, WorkforceTeam Team, IActionResult Denied)> AuthorizePeriodAsync(string id, global::OrchardCore.Security.Permissions.Permission permission = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, permission ?? WorkforcePermissions.ManageSchedules))
        {
            return (null, null, Forbid());
        }

        var period = await _planningService.FindPeriodAsync(id, HttpContext.RequestAborted);
        var team = period is null ? null : await _teamManager.FindByIdAsync(period.TeamId);

        if (period is null || team is null)
        {
            return (null, null, NotFound());
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return scope.Covers(team.ItemId) ? (period, team, null) : (null, null, Forbid());
    }

    private async Task<DraftShift> FindDraftAsync(SchedulePeriod period, string id)
    {
        return (await _planningService.ListDraftShiftsAsync(period.PrimaryScenarioId, HttpContext.RequestAborted))
            .FirstOrDefault(shift => shift.ItemId == id);
    }

    private async Task<string> TimeZoneOfAsync(WorkforceTeam team)
    {
        if (!string.IsNullOrEmpty(team.TimeZoneId))
        {
            return team.TimeZoneId;
        }

        var timeZoneId = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;

        return string.IsNullOrEmpty(timeZoneId) ? "UTC" : timeZoneId;
    }

    private static int WeekOf(SchedulePeriod period, DateOnly date)
        => Math.Max(0, (date.DayNumber - period.StartDate.DayNumber) / 7);

    private async Task AddCoverageAsync(ScheduleBoardViewModel model, SchedulePeriod period, WorkforceTeam team)
    {
        if (model.Days.Count == 0)
        {
            return;
        }

        var timeZoneId = await TimeZoneOfAsync(team);
        var intervals = await _coverageService.ComputeAsync(new CoverageQuery
        {
            TeamId = team.ItemId,
            From = model.Days[0],
            To = model.Days[^1],
            IntervalMinutes = CoverageIntervalMinutes,
            ScenarioId = period.PrimaryScenarioId,
        }, HttpContext.RequestAborted);

        foreach (var day in model.Days)
        {
            model.Coverage[day] = new ScheduleDayCoverageViewModel();
        }

        foreach (var interval in intervals.Where(interval => interval.Target > 0 || interval.Minimum > 0))
        {
            var local = CalendarTime.ToLocal(interval.StartUtc, timeZoneId);
            var day = DateOnly.FromDateTime(local);

            if (!model.Coverage.TryGetValue(day, out var coverage))
            {
                continue;
            }

            coverage.HasRequirement = true;

            if (interval.Scheduled < interval.Minimum)
            {
                coverage.ShortIntervals++;

                if (interval.Minimum - interval.Scheduled > coverage.WorstShort)
                {
                    coverage.WorstShort = interval.Minimum - interval.Scheduled;
                    coverage.WorstShortAt = TimeOnly.FromDateTime(local);
                }
            }

            if (interval.Gap > 0)
            {
                coverage.UnderTargetIntervals++;
            }

            if (interval.Gap > coverage.WorstGap)
            {
                coverage.WorstGap = interval.Gap;
                coverage.WorstAt = TimeOnly.FromDateTime(local);
            }
        }
    }

    private async Task PopulateShiftAsync(DraftShiftEditViewModel model, SchedulePeriod period, WorkforceTeam team, IEnumerable<ActivityCode> codes)
    {
        var day = model.Date ?? period.StartDate;
        var workers = (await _workerManager.ListByTeamAsync(team.ItemId, day, HttpContext.RequestAborted)).ToList();

        if (!string.IsNullOrEmpty(model.WorkerId) && workers.All(worker => worker.ItemId != model.WorkerId))
        {
            var current = await _workerManager.FindByIdAsync(model.WorkerId);

            if (current is not null)
            {
                workers.Add(current);
            }
        }

        model.PeriodName = period.Name;
        model.ActivityNames = codes.Where(code => code.Enabled).Select(code => code.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        model.WorkerOptions = workers
            .Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == model.WorkerId))
            .ToList();
        model.TemplateOptions = (await _templateManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(template => template.Enabled && (template.TeamIds.Count == 0 || template.TeamIds.Contains(team.ItemId)))
            .OrderBy(template => template.Start)
            .Select(template => new SelectListItem($"{template.Name} ({template.Start:HH\\:mm}-{template.Start.AddMinutes(template.LengthMinutes):HH\\:mm})", template.ItemId, template.ItemId == model.TemplateId))
            .ToList();
    }
}
