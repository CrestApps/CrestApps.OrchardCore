using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// The real-time adherence board and a worker's adherence over a day.
/// </summary>
public sealed class AdherenceController : Controller
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
    /// Initializes a new instance of the <see cref="AdherenceController"/> class.
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
    public AdherenceController(
        IAuthorizationService authorizationService,
        AdherenceService adherenceService,
        IWorkforceScopeService scopeService,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<AdherenceController> htmlLocalizer)
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
    /// Shows the board.
    /// </summary>
    /// <param name="teamId">The team, or <see langword="null"/> for every team the viewer manages.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence", "WorkforceAdherence")]
    public async Task<IActionResult> Index(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewAdherence))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return View(new AdherenceBoardViewModel
        {
            TeamId = teamId,
            TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, scope, [teamId], HttpContext.RequestAborted),
            Rows = await RowsAsync(teamId),
            CanManage = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence),
        });
    }

    /// <summary>
    /// Gets the board's rows, for the page to refresh itself.
    /// </summary>
    /// <param name="teamId">The team, or <see langword="null"/>.</param>
    /// <returns>The rows.</returns>
    [Admin("workforce/adherence/rows", "WorkforceAdherenceRows")]
    public async Task<IActionResult> Rows(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewAdherence))
        {
            return Forbid();
        }

        return PartialView("_AdherenceRows", new AdherenceBoardViewModel
        {
            TeamId = teamId,
            Rows = await RowsAsync(teamId),
            CanManage = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence),
        });
    }

    /// <summary>
    /// Shows a worker's adherence over a day: the saved day, or the day computed now when none is saved.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day; today when none is given.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence/worker/{workerId}", "WorkforceAdherenceWorker")]
    public async Task<IActionResult> Day(string workerId, DateOnly? date)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ViewAdherence))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId, HttpContext.RequestAborted);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var day = date ?? DateOnly.FromDateTime(CalendarTime.ToLocal(_clock.UtcNow, await _adherenceService.TimeZoneOfAsync(worker, HttpContext.RequestAborted)));
        var saved = await _adherenceService.FindDayAsync(worker.ItemId, day, HttpContext.RequestAborted);

        return View(new AdherenceDayViewModel
        {
            Worker = worker,
            Date = day,
            Day = saved ?? await _adherenceService.ComputeDayAsync(worker, day, HttpContext.RequestAborted),
            Saved = saved is not null,
            CanManage = await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence),
        });
    }

    /// <summary>
    /// Computes a worker's day again and saves it.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day.</param>
    /// <returns>The day.</returns>
    [HttpPost]
    [Admin("workforce/adherence/worker/{workerId}/save", "WorkforceAdherenceWorkerSave")]
    public async Task<IActionResult> SaveDay(string workerId, DateOnly date)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var worker = await _workerManager.FindByIdAsync(workerId, HttpContext.RequestAborted);

        if (worker is null)
        {
            return NotFound();
        }

        if (!await _scopeService.ManagesWorkerAsync(User, worker, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (await _adherenceService.SaveDayAsync(worker, date, HttpContext.RequestAborted) is null)
        {
            await _notifier.WarningAsync(H["{0} is not a contact center agent.", worker.DisplayName]);
        }
        else
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The day was computed again and saved."]);
        }

        return RedirectToAction(nameof(Day), new { workerId, date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
    }

    private async Task<IReadOnlyList<AdherenceBoardItem>> RowsAsync(string teamId)
    {
        var now = _clock.UtcNow;
        var rows = await _adherenceService.BoardAsync(User, string.IsNullOrEmpty(teamId) ? null : teamId, _scopeService, HttpContext.RequestAborted);
        var items = new List<AdherenceBoardItem>(rows.Count);

        foreach (var row in rows)
        {
            items.Add(new AdherenceBoardItem
            {
                Row = row,
                TimeZoneId = row.TimeZoneId ?? "UTC",
                OutMinutes = row.OutSinceUtc is { } outSince ? (int)Math.Max(0, (now - outSince).TotalMinutes) : 0,
                StateMinutes = row.StatusSinceUtc is { } since ? (int)Math.Max(0, (now - since).TotalMinutes) : 0,
            });
        }

        return items;
    }
}
