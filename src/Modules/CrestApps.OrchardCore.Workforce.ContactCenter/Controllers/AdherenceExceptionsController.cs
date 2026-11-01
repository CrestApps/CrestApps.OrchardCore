using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// Adherence exceptions: stretches of a worker's schedule left out of their adherence.
/// </summary>
public sealed class AdherenceExceptionsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AdherenceService _adherenceService;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceExceptionsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="adherenceService">The adherence service.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AdherenceExceptionsController(
        IAuthorizationService authorizationService,
        AdherenceService adherenceService,
        IWorkforceScopeService scopeService,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<AdherenceExceptionsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _adherenceService = adherenceService;
        _scopeService = scopeService;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the exceptions of the teams the viewer manages, newest first.
    /// </summary>
    /// <param name="teamId">The team filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence/exceptions", "WorkforceAdherenceExceptions")]
    public async Task<IActionResult> Index(string teamId, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewAdherence))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = null;

        if (!string.IsNullOrEmpty(teamId) && scope.Covers(teamId))
        {
            teamIds = [.. await _teamManager.GetSubtreeIdsAsync(teamId, HttpContext.RequestAborted)];
        }
        else if (!scope.All)
        {
            teamIds = [.. scope.TeamIds];
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _adherenceService.PageExceptionsAsync(teamIds, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var teams = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team, StringComparer.Ordinal);
        var canManage = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence);
        var rows = new List<AdherenceExceptionRow>(items.Count);

        foreach (var item in items)
        {
            var worker = await _workerManager.FindByIdAsync(item.WorkerId, HttpContext.RequestAborted);
            var team = item.TeamId is not null && teams.TryGetValue(item.TeamId, out var found) ? found : null;

            rows.Add(new AdherenceExceptionRow
            {
                Exception = item,
                WorkerName = worker?.DisplayName ?? item.WorkerId,
                TeamName = team?.Name,
                TimeZoneId = await ZoneOfAsync(worker),
                CanManage = canManage,
            });
        }

        return View(new AdherenceExceptionListViewModel
        {
            TeamId = teamId,
            TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, scope, [teamId], HttpContext.RequestAborted),
            CanManage = canManage,
            List = new AdminListContext<AdherenceExceptionRow>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }

    /// <summary>
    /// Shows the form that records an exception.
    /// </summary>
    /// <param name="workerId">The worker, when known.</param>
    /// <param name="date">The day, when known.</param>
    /// <param name="returnUrl">Where to go back to.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence/exceptions/add", "WorkforceAdherenceExceptionsAdd")]
    public async Task<IActionResult> Create(string workerId, DateOnly? date, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var model = new AdherenceExceptionEditViewModel { WorkerId = workerId, Reason = AdherenceExceptionReason.Coaching, ReturnUrl = returnUrl };
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerManager.FindByIdAsync(workerId, HttpContext.RequestAborted);
        var local = CalendarTime.ToLocal(now, await ZoneOfAsync(worker));

        model.Date = date ?? DateOnly.FromDateTime(local);
        model.Start = date.HasValue ? new TimeOnly(9, 0) : TimeOnly.FromDateTime(local.AddMinutes(-(local.Minute % 15)));
        model.End = model.Start.AddMinutes(30);

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Records an exception.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>The list, or the form with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/adherence/exceptions/add", "WorkforceAdherenceExceptionsAdd")]
    public async Task<IActionResult> CreatePost(AdherenceExceptionEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var worker = string.IsNullOrEmpty(model.WorkerId) ? null : await _workerManager.FindByIdAsync(model.WorkerId, HttpContext.RequestAborted);

        if (worker is null || !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            ModelState.AddModelError(nameof(model.WorkerId), H["Choose a worker on a team you manage."].Value);
        }
        else
        {
            var zone = await ZoneOfAsync(worker);
            var end = model.End <= model.Start ? model.Date.AddDays(1).ToDateTime(model.End) : model.Date.ToDateTime(model.End);
            var errors = await _adherenceService.AddExceptionAsync(new AdherenceException
            {
                WorkerId = worker.ItemId,
                StartUtc = CalendarTime.ToUtc(model.Date.ToDateTime(model.Start), zone),
                EndUtc = CalendarTime.ToUtc(end, zone),
                Reason = model.Reason,
                Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
                RecordedBy = User.Identity?.Name,
            }, HttpContext.RequestAborted);

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);

            return View(model);
        }

        await _session.SaveChangesAsync(HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The exception was recorded. It counts once the worker's day is saved; the history saves each day after it ends."]);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes an exception.
    /// </summary>
    /// <param name="id">The exception.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/adherence/exceptions/delete/{id}", "WorkforceAdherenceExceptionsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var exception = await _adherenceService.FindExceptionAsync(id, HttpContext.RequestAborted);

        if (exception is null)
        {
            return NotFound();
        }

        var worker = await _workerManager.FindByIdAsync(exception.WorkerId, HttpContext.RequestAborted);

        if (worker is not null && !await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        _adherenceService.RemoveException(exception);
        await _session.SaveChangesAsync(HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The exception was removed."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(AdherenceExceptionEditViewModel model)
    {
        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var (_, workers) = await _workerManager.SearchAsync(new WorkerQuery { HasUser = true }, 0, 1000, HttpContext.RequestAborted);

        model.WorkerOptions = workers
            .Where(worker => scope.All || worker.TeamMemberships.Any(membership => scope.TeamIds.Contains(membership.TeamId)))
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == model.WorkerId))
            .ToList();
    }

    private async Task<string> ZoneOfAsync(WorkerProfile worker)
        => worker is null ? "UTC" : await _adherenceService.TimeZoneOfAsync(worker, HttpContext.RequestAborted);
}
