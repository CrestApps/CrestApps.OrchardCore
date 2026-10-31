using System.Globalization;
using CrestApps.OrchardCore.Skills.Services;
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

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The open shifts of the teams the user manages: the list, posting one on its own, and deciding on claims.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class OpenShiftsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IOpenShiftService _openShiftService;
    private readonly IShiftTemplateManager _templateManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ISkillManager _skillManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenShiftsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="openShiftService">The open shift service.</param>
    /// <param name="templateManager">The shift template manager.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenShiftsController(
        IAuthorizationService authorizationService,
        IOpenShiftService openShiftService,
        IShiftTemplateManager templateManager,
        IActivityCodeManager activityCodeManager,
        ISkillManager skillManager,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IWorkforceScopeService scopeService,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<OpenShiftsController> htmlLocalizer,
        IStringLocalizer<OpenShiftsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _openShiftService = openShiftService;
        _templateManager = templateManager;
        _activityCodeManager = activityCodeManager;
        _skillManager = skillManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _scopeService = scopeService;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the open shifts of the teams the user manages, soonest first.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/open-shifts", "WorkforceOpenShifts")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        OpenShiftStatus? status = Enum.TryParse<OpenShiftStatus>(model.Status, out var parsed) ? parsed : null;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, shifts) = await _openShiftService.PageAsync(new OpenShiftQuery { TeamIds = teamIds, Status = status }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<OpenShiftRowViewModel>();

        foreach (var shift in shifts)
        {
            rows.Add(new OpenShiftRowViewModel
            {
                Shift = shift,
                TeamName = (await _teamManager.FindByIdAsync(shift.TeamId))?.Name,
                FilledWorkerName = string.IsNullOrEmpty(shift.FilledWorkerId) ? null : (await _workerManager.FindByIdAsync(shift.FilledWorkerId))?.DisplayName,
            });
        }

        model.List = new AdminListContext<OpenShiftRowViewModel>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId }, { "status", model.Status } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["Open and claimed"], string.Empty, status is null),
            new SelectListItem(S["Open"], nameof(OpenShiftStatus.Open), status == OpenShiftStatus.Open),
            new SelectListItem(S["Claimed"], nameof(OpenShiftStatus.Claimed), status == OpenShiftStatus.Claimed),
            new SelectListItem(S["Filled"], nameof(OpenShiftStatus.Filled), status == OpenShiftStatus.Filled),
            new SelectListItem(S["Canceled"], nameof(OpenShiftStatus.Canceled), status == OpenShiftStatus.Canceled),
        ];

        return View(model);
    }

    /// <summary>
    /// Shows the form that posts an open shift on its own.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <returns>The form.</returns>
    [Admin("workforce/open-shifts/create", "WorkforceOpenShiftsCreate")]
    public async Task<IActionResult> Create(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts))
        {
            return Forbid();
        }

        var model = new OpenShiftEditViewModel
        {
            TeamId = teamId,
            Date = (await PlanningOptions.TodayAsync(_localClock)).AddDays(1),
            Start = "09:00",
            End = "17:00",
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Posts an open shift on its own.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <param name="applyTemplate">Whether to take the times and breaks from the chosen template.</param>
    /// <returns>A redirect to the open shift, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/open-shifts/create", "WorkforceOpenShiftsCreate")]
    public async Task<IActionResult> CreatePost(OpenShiftEditViewModel model, bool applyTemplate = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!string.IsNullOrEmpty(model.TeamId) && !scope.Covers(model.TeamId))
        {
            return Forbid();
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

            await PopulateAsync(model);

            return View(model);
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
            var result = await _openShiftService.PostAsync(new OpenShift
            {
                TeamId = model.TeamId,
                Date = model.Date.Value,
                Start = from,
                End = to,
                TemplateId = template?.ItemId,
                Segments = SegmentFormat.ToDraft(segments, from),
                RequiredSkillIds = template?.RequiredSkillIds.ToList() ?? [],
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            }, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The open shift has been posted."]);

                return RedirectToAction(nameof(Details), new { id = result.Value.ItemId });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Shows an open shift with its claims and the workers who can take it.
    /// </summary>
    /// <param name="id">The open shift id.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/open-shifts/details/{id}", "WorkforceOpenShiftsDetails")]
    public async Task<IActionResult> Details(string id)
    {
        var (shift, team, denied) = await AuthorizeShiftAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var template = string.IsNullOrEmpty(shift.TemplateId) ? null : await _templateManager.FindByIdAsync(shift.TemplateId);
        var model = new OpenShiftDetailsViewModel
        {
            Shift = shift,
            TeamName = team.Name,
            TemplateName = template?.Name,
            FilledWorkerName = string.IsNullOrEmpty(shift.FilledWorkerId) ? null : (await _workerManager.FindByIdAsync(shift.FilledWorkerId))?.DisplayName,
        };

        foreach (var skillId in shift.RequiredSkillIds)
        {
            model.SkillNames.Add((await _skillManager.FindByIdAsync(skillId))?.Name ?? skillId);
        }

        var open = shift.Status is OpenShiftStatus.Open or OpenShiftStatus.Claimed;

        foreach (var claim in shift.Claims.OrderByDescending(claim => claim.ClaimedUtc))
        {
            var worker = await _workerManager.FindByIdAsync(claim.WorkerId);

            model.Claims.Add(new OpenShiftClaimRowViewModel
            {
                Claim = claim,
                WorkerName = worker?.DisplayName ?? claim.WorkerId,
                SeniorityDate = worker?.SeniorityDate ?? worker?.HireDate,
                Problems = open && claim.Status == OpenShiftClaimStatus.Pending
                    ? (await _openShiftService.CheckAsync(shift.ItemId, claim.WorkerId, HttpContext.RequestAborted)).ToList()
                    : [],
            });
        }

        if (open)
        {
            model.EligibleOptions = (await _openShiftService.ListEligibleWorkersAsync(shift.ItemId, HttpContext.RequestAborted))
                .Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId))
                .ToList();
        }

        return View(model);
    }

    /// <summary>
    /// Gives an open shift to a worker, approving their claim if they made one.
    /// </summary>
    /// <param name="id">The open shift id.</param>
    /// <param name="workerId">The worker.</param>
    /// <returns>A redirect to the open shift.</returns>
    [HttpPost]
    [Admin("workforce/open-shifts/assign/{id}", "WorkforceOpenShiftsAssign")]
    public async Task<IActionResult> Assign(string id, string workerId)
    {
        var (shift, _, denied) = await AuthorizeShiftAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _openShiftService.AssignAsync(shift.ItemId, workerId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The shift has been given to the worker and put on the calendar."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Records a claim on a worker's behalf.
    /// </summary>
    /// <param name="id">The open shift id.</param>
    /// <param name="workerId">The worker.</param>
    /// <returns>A redirect to the open shift.</returns>
    [HttpPost]
    [Admin("workforce/open-shifts/claim/{id}", "WorkforceOpenShiftsClaim")]
    public async Task<IActionResult> Claim(string id, string workerId)
    {
        var (shift, _, denied) = await AuthorizeShiftAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _openShiftService.ClaimAsync(shift.ItemId, workerId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, result.Value?.Status == OpenShiftStatus.Filled ? H["The claim filled the shift."] : H["The claim has been recorded."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Turns down a claim.
    /// </summary>
    /// <param name="id">The open shift id.</param>
    /// <param name="workerId">The claimant.</param>
    /// <param name="note">Why.</param>
    /// <returns>A redirect to the open shift.</returns>
    [HttpPost]
    [Admin("workforce/open-shifts/decline/{id}", "WorkforceOpenShiftsDecline")]
    public async Task<IActionResult> Decline(string id, string workerId, string note)
    {
        var (shift, _, denied) = await AuthorizeShiftAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _openShiftService.DeclineClaimAsync(shift.ItemId, workerId, note, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The claim has been declined."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Stops offering an open shift.
    /// </summary>
    /// <param name="id">The open shift id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/open-shifts/cancel/{id}", "WorkforceOpenShiftsCancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        var (shift, _, denied) = await AuthorizeShiftAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (await _openShiftService.CancelAsync(shift.ItemId, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The open shift is no longer offered."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task NotifyAsync(IReadOnlyList<string> errors, LocalizedHtmlString success)
    {
        foreach (var error in errors)
        {
            await _notifier.WarningAsync(H["{0}", error]);
        }

        if (errors.Count == 0)
        {
            await _notifier.SuccessAsync(success);
        }
    }

    private async Task<(OpenShift Shift, WorkforceTeam Team, IActionResult Denied)> AuthorizeShiftAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageOpenShifts))
        {
            return (null, null, Forbid());
        }

        var shift = await _openShiftService.FindAsync(id, HttpContext.RequestAborted);
        var team = shift is null ? null : await _teamManager.FindByIdAsync(shift.TeamId);

        if (shift is null || team is null)
        {
            return (null, null, NotFound());
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return scope.Covers(team.ItemId) ? (shift, team, null) : (null, null, Forbid());
    }

    private async Task PopulateAsync(OpenShiftEditViewModel model)
    {
        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.ActivityNames = (await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted)).Where(code => code.Enabled).Select(code => code.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        model.TemplateOptions = (await _templateManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(template => template.Enabled && (template.TeamIds.Count == 0 || string.IsNullOrEmpty(model.TeamId) || template.TeamIds.Contains(model.TeamId)))
            .OrderBy(template => template.Start)
            .Select(template => new SelectListItem($"{template.Name} ({template.Start:HH\\:mm}-{template.Start.AddMinutes(template.LengthMinutes):HH\\:mm})", template.ItemId, template.ItemId == model.TemplateId))
            .ToList();
    }
}
