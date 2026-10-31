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
/// The rotations: repeating weekly patterns of shift templates that fill a team's schedule.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class RotationsController : Controller
{
    private const int MaxWeeks = 12;

    private readonly IAuthorizationService _authorizationService;
    private readonly IShiftPatternManager _patternManager;
    private readonly IShiftTemplateManager _templateManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RotationsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="patternManager">The rotation manager.</param>
    /// <param name="templateManager">The shift template manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RotationsController(
        IAuthorizationService authorizationService,
        IShiftPatternManager patternManager,
        IShiftTemplateManager templateManager,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IWorkforceScopeService scopeService,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<RotationsController> htmlLocalizer,
        IStringLocalizer<RotationsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _patternManager = patternManager;
        _templateManager = templateManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _scopeService = scopeService;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the rotations of the teams the user manages.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/rotations", "Rotations")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var patterns = (await _patternManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(pattern => scope.Covers(pattern.TeamId) && (string.IsNullOrEmpty(model.TeamId) || pattern.TeamId == model.TeamId))
            .OrderBy(pattern => pattern.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        model.List = new AdminListContext<ShiftPattern>
        {
            Source = patterns.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, patterns.Count, new RouteValueDictionary { { "teamId", model.TeamId } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);

        return View(model);
    }

    /// <summary>
    /// Shows the editor of a new or existing rotation.
    /// </summary>
    /// <param name="id">The rotation id, or empty for a new rotation.</param>
    /// <param name="teamId">The team of a new rotation.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/rotations/edit/{id?}", "RotationsEdit")]
    public async Task<IActionResult> Edit(string id, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var pattern = string.IsNullOrEmpty(id)
            ? await _patternManager.NewAsync()
            : await _patternManager.FindByIdAsync(id);

        if (pattern is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(id))
        {
            pattern.TeamId = teamId;
        }
        else if (!scope.Covers(pattern.TeamId))
        {
            return Forbid();
        }

        var slots = new string[MaxWeeks * 7];

        foreach (var day in pattern.Days.Where(day => day.Week is >= 0 and < MaxWeeks))
        {
            slots[(day.Week * 7) + PlanningOptions.DayIndex(day.Day)] = day.TemplateId;
        }

        var model = new RotationEditViewModel
        {
            Id = id,
            Name = pattern.Name,
            TeamId = pattern.TeamId,
            WeekCount = Math.Max(1, pattern.WeekCount),
            Slots = slots,
            WorkerIds = pattern.Workers.Select(worker => worker.WorkerId).ToArray(),
            StartDate = await PlanningOptions.NextMondayAsync(_localClock),
            Enabled = pattern.Enabled,
        };

        await PopulateAsync(model, scope, pattern);

        return View(model);
    }

    /// <summary>
    /// Saves a rotation.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/rotations/edit/{id?}", "RotationsEdit")]
    public async Task<IActionResult> EditPost(RotationEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var pattern = isNew
            ? await _patternManager.NewAsync()
            : await _patternManager.FindByIdAsync(model.Id);

        if (pattern is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if ((!isNew && !scope.Covers(pattern.TeamId)) || (!string.IsNullOrEmpty(model.TeamId) && !scope.Covers(model.TeamId)))
        {
            return Forbid();
        }

        var weeks = Math.Clamp(model.WeekCount, 1, MaxWeeks);
        var slots = model.Slots ?? [];
        var startDate = model.StartDate ?? await PlanningOptions.NextMondayAsync(_localClock);
        var chosen = (model.WorkerIds ?? []).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        var existing = pattern.Workers.ToDictionary(worker => worker.WorkerId, StringComparer.Ordinal);

        pattern.Name = model.Name;
        pattern.TeamId = model.TeamId;
        pattern.WeekCount = weeks;
        pattern.Enabled = model.Enabled;
        pattern.Days = Enumerable.Range(0, weeks * 7)
            .Where(index => index < slots.Length && !string.IsNullOrEmpty(slots[index]))
            .Select(index => new ShiftPatternDay { Week = index / 7, Day = PlanningOptions.DayOf(index % 7), TemplateId = slots[index] })
            .ToList();
        pattern.Workers = chosen
            .Select(workerId => existing.TryGetValue(workerId, out var follower) ? follower : new ShiftPatternWorker { WorkerId = workerId, StartDate = startDate })
            .ToList();

        var validation = await _patternManager.ValidateAsync(pattern);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            model.Slots = Enumerable.Range(0, MaxWeeks * 7).Select(index => index < slots.Length ? slots[index] : null).ToArray();
            await PopulateAsync(model, scope, pattern);

            return View(model);
        }

        if (isNew)
        {
            await _patternManager.CreateAsync(pattern);
        }
        else
        {
            await _patternManager.UpdateAsync(pattern);
        }

        await _notifier.SuccessAsync(H["The rotation has been saved."]);

        return RedirectToAction(nameof(Index), new { teamId = pattern.TeamId });
    }

    /// <summary>
    /// Deletes a rotation.
    /// </summary>
    /// <param name="id">The rotation id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/rotations/delete/{id}", "RotationsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var pattern = await _patternManager.FindByIdAsync(id);

        if (pattern is not null)
        {
            var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

            if (!scope.Covers(pattern.TeamId))
            {
                return Forbid();
            }

            await _patternManager.DeleteAsync(pattern);
            await _notifier.SuccessAsync(H["The rotation has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(RotationEditViewModel model, WorkforceScope scope, ShiftPattern pattern)
    {
        var chosen = (model.WorkerIds ?? []).ToHashSet(StringComparer.Ordinal);
        var today = await PlanningOptions.TodayAsync(_localClock);

        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.TemplateOptions = (await _templateManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(template => template.Enabled && (template.TeamIds.Count == 0 || template.TeamIds.Contains(model.TeamId)))
            .OrderBy(template => template.Start)
            .ThenBy(template => template.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(template => new SelectListItem($"{template.Name} ({template.Start:HH\\:mm}-{template.Start.AddMinutes(template.LengthMinutes):HH\\:mm})", template.ItemId))
            .ToList();

        var workers = string.IsNullOrEmpty(model.TeamId)
            ? []
            : await _workerManager.ListByTeamAsync(model.TeamId, today, HttpContext.RequestAborted);

        model.WorkerOptions = workers
            .Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, chosen.Contains(worker.ItemId)))
            .ToList();

        var names = workers.ToDictionary(worker => worker.ItemId, worker => worker.DisplayName, StringComparer.Ordinal);

        model.Workers = pattern.Workers
            .Where(worker => names.ContainsKey(worker.WorkerId))
            .Select(worker => new RotationWorkerViewModel { Name = names[worker.WorkerId], StartDate = worker.StartDate })
            .OrderBy(worker => worker.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
