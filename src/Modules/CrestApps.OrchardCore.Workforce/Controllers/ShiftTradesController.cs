using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
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
/// The shift trades of the teams the user manages: the list, offering one on a worker's behalf, accepting for a
/// colleague, and approving or declining.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class ShiftTradesController : Controller
{
    private const int UpcomingDays = 60;

    private readonly IAuthorizationService _authorizationService;
    private readonly IShiftTradeService _tradeService;
    private readonly IShiftService _shiftService;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTradesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="tradeService">The shift trade service.</param>
    /// <param name="shiftService">The published shift service.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftTradesController(
        IAuthorizationService authorizationService,
        IShiftTradeService tradeService,
        IShiftService shiftService,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IWorkforceScopeService scopeService,
        ISiteService siteService,
        IClock clock,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<ShiftTradesController> htmlLocalizer,
        IStringLocalizer<ShiftTradesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _tradeService = tradeService;
        _shiftService = shiftService;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _scopeService = scopeService;
        _siteService = siteService;
        _clock = clock;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the trades of the teams the user manages, newest first.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/shift-trades", "WorkforceShiftTrades")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        ShiftTradeStatus? status = Enum.TryParse<ShiftTradeStatus>(model.Status, out var parsed) ? parsed : null;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, trades) = await _tradeService.PageAsync(new ShiftTradeQuery { TeamIds = teamIds, Status = status }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<ShiftTradeRowViewModel>();

        foreach (var trade in trades)
        {
            rows.Add(await ToRowAsync(trade));
        }

        model.List = new AdminListContext<ShiftTradeRowViewModel>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId }, { "status", model.Status } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["Any status"], string.Empty, status is null),
            new SelectListItem(S["Offered"], nameof(ShiftTradeStatus.Offered), status == ShiftTradeStatus.Offered),
            new SelectListItem(S["Waiting for approval"], nameof(ShiftTradeStatus.Accepted), status == ShiftTradeStatus.Accepted),
            new SelectListItem(S["Approved"], nameof(ShiftTradeStatus.Approved), status == ShiftTradeStatus.Approved),
            new SelectListItem(S["Declined"], nameof(ShiftTradeStatus.Declined), status == ShiftTradeStatus.Declined),
            new SelectListItem(S["Turned down"], nameof(ShiftTradeStatus.Rejected), status == ShiftTradeStatus.Rejected),
            new SelectListItem(S["Canceled"], nameof(ShiftTradeStatus.Canceled), status == ShiftTradeStatus.Canceled),
        ];

        return View(model);
    }

    /// <summary>
    /// Shows the form that offers a trade on a worker's behalf.
    /// </summary>
    /// <param name="workerId">The worker offering a shift.</param>
    /// <returns>The form.</returns>
    [Admin("workforce/shift-trades/create", "WorkforceShiftTradesCreate")]
    public async Task<IActionResult> Create(string workerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades))
        {
            return Forbid();
        }

        var model = new ShiftTradeCreateViewModel { WorkerId = workerId, Kind = nameof(ShiftTradeKind.GiveAway) };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Offers a trade on a worker's behalf, or reloads the form after a choice changed.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <param name="refresh">Whether only to reload the choices.</param>
    /// <returns>A redirect to the trade, or the form.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("workforce/shift-trades/create", "WorkforceShiftTradesCreate")]
    public async Task<IActionResult> CreatePost(ShiftTradeCreateViewModel model, bool refresh = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades))
        {
            return Forbid();
        }

        if (refresh)
        {
            ModelState.Clear();
            await PopulateAsync(model);

            return View(model);
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var worker = string.IsNullOrEmpty(model.WorkerId) ? null : await _workerManager.FindByIdAsync(model.WorkerId);

        if (worker is null || !worker.TeamMemberships.Any(membership => scope.Covers(membership.TeamId)))
        {
            ModelState.AddModelError(nameof(model.WorkerId), S["Choose a worker on a team you manage."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _tradeService.OfferAsync(new ShiftTradeOffer
            {
                Kind = Enum.TryParse<ShiftTradeKind>(model.Kind, out var kind) ? kind : ShiftTradeKind.GiveAway,
                ShiftEventId = model.ShiftEventId,
                TargetWorkerId = string.IsNullOrEmpty(model.TargetWorkerId) ? null : model.TargetWorkerId,
                TargetShiftEventId = string.IsNullOrEmpty(model.TargetShiftEventId) ? null : model.TargetShiftEventId,
                Note = model.Note,
            }, User.Identity?.Name, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The trade has been offered."]);

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
    /// Shows a trade and who can accept it.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/shift-trades/details/{id}", "WorkforceShiftTradesDetails")]
    public async Task<IActionResult> Details(string id)
    {
        var (trade, team, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var model = new ShiftTradeDetailsViewModel { Row = await ToRowAsync(trade) };

        if (trade.Status == ShiftTradeStatus.Offered)
        {
            var date = DateOnly.FromDateTime(CalendarTime.ToLocal(trade.ShiftStartUtc, model.Row.TimeZoneId));
            var candidates = string.IsNullOrEmpty(trade.TargetWorkerId)
                ? (await _workerManager.ListByTeamAsync(team.ItemId, date, HttpContext.RequestAborted)).Where(worker => worker.ItemId != trade.OfferedByWorkerId && worker.Status == WorkerStatus.Active).ToList()
                : [await _workerManager.FindByIdAsync(trade.TargetWorkerId)];

            foreach (var candidate in candidates.Where(candidate => candidate is not null))
            {
                model.Candidates.Add(new ShiftTradeCandidateViewModel
                {
                    WorkerId = candidate.ItemId,
                    Name = candidate.DisplayName,
                    Problems = (await _tradeService.CheckAsync(trade.ItemId, candidate.ItemId, HttpContext.RequestAborted)).ToList(),
                });
            }
        }

        return View(model);
    }

    /// <summary>
    /// Accepts a trade on a colleague's behalf.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <param name="workerId">The colleague.</param>
    /// <returns>A redirect to the trade.</returns>
    [HttpPost]
    [Admin("workforce/shift-trades/accept/{id}", "WorkforceShiftTradesAccept")]
    public async Task<IActionResult> Accept(string id, string workerId)
    {
        var (trade, _, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _tradeService.AcceptAsync(trade.ItemId, workerId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, result.Value?.Status == ShiftTradeStatus.Approved ? H["The trade was accepted and the shifts changed hands."] : H["The trade was accepted and waits for approval."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Turns a trade down on behalf of the colleague it was offered to.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <returns>A redirect to the trade.</returns>
    [HttpPost]
    [Admin("workforce/shift-trades/reject/{id}", "WorkforceShiftTradesReject")]
    public async Task<IActionResult> Reject(string id)
    {
        var (trade, _, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _tradeService.RejectAsync(trade.ItemId, trade.TargetWorkerId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The trade has been turned down."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Approves an accepted trade, which applies it.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <returns>A redirect to the trade.</returns>
    [HttpPost]
    [Admin("workforce/shift-trades/approve/{id}", "WorkforceShiftTradesApprove")]
    public async Task<IActionResult> Approve(string id)
    {
        var (trade, _, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _tradeService.ApproveAsync(trade.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The trade has been approved and the shifts changed hands."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Declines a trade.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <param name="note">Why.</param>
    /// <returns>A redirect to the trade.</returns>
    [HttpPost]
    [Admin("workforce/shift-trades/decline/{id}", "WorkforceShiftTradesDecline")]
    public async Task<IActionResult> Decline(string id, string note)
    {
        var (trade, _, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _tradeService.DeclineAsync(trade.ItemId, note, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The trade has been declined."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Takes back a trade on the offering worker's behalf.
    /// </summary>
    /// <param name="id">The trade id.</param>
    /// <returns>A redirect to the trade.</returns>
    [HttpPost]
    [Admin("workforce/shift-trades/cancel/{id}", "WorkforceShiftTradesCancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        var (trade, _, denied) = await AuthorizeTradeAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var result = await _tradeService.CancelAsync(trade.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["The trade has been canceled."]);

        return RedirectToAction(nameof(Details), new { id });
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

    private async Task<(ShiftTrade Trade, WorkforceTeam Team, IActionResult Denied)> AuthorizeTradeAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApproveShiftTrades))
        {
            return (null, null, Forbid());
        }

        var trade = await _tradeService.FindAsync(id, HttpContext.RequestAborted);
        var team = trade is null ? null : await _teamManager.FindByIdAsync(trade.TeamId);

        if (trade is null || team is null)
        {
            return (null, null, NotFound());
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        return scope.Covers(team.ItemId) ? (trade, team, null) : (null, null, Forbid());
    }

    private async Task<ShiftTradeRowViewModel> ToRowAsync(ShiftTrade trade)
    {
        var team = await _teamManager.FindByIdAsync(trade.TeamId);

        return new ShiftTradeRowViewModel
        {
            Trade = trade,
            TeamName = team?.Name,
            OfferedByName = await NameAsync(trade.OfferedByWorkerId),
            TargetName = await NameAsync(trade.TargetWorkerId),
            AcceptedByName = await NameAsync(trade.AcceptedByWorkerId),
            TimeZoneId = await PlanningOptions.TimeZoneOfAsync(team, _siteService),
        };
    }

    private async Task<string> NameAsync(string workerId)
    {
        return string.IsNullOrEmpty(workerId) ? null : (await _workerManager.FindByIdAsync(workerId))?.DisplayName ?? workerId;
    }

    private async Task PopulateAsync(ShiftTradeCreateViewModel model)
    {
        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var today = await PlanningOptions.TodayAsync(_localClock);
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

        foreach (var item in (await _teamManager.ListTreeAsync(HttpContext.RequestAborted)).Where(item => scope.Covers(item.Team.ItemId)))
        {
            foreach (var worker in await _workerManager.ListByTeamAsync(item.Team.ItemId, today, HttpContext.RequestAborted))
            {
                workers.TryAdd(worker.ItemId, worker);
            }
        }

        var ordered = workers.Values.Where(worker => worker.Status == WorkerStatus.Active).OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

        model.WorkerOptions = ordered.Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == model.WorkerId)).ToList();
        model.ShiftOptions = await ShiftOptionsAsync(model.WorkerId, model.ShiftEventId);
        model.TargetOptions = ordered.Where(worker => worker.ItemId != model.WorkerId).Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == model.TargetWorkerId)).ToList();
        model.TargetShiftOptions = model.Kind == nameof(ShiftTradeKind.Swap) ? await ShiftOptionsAsync(model.TargetWorkerId, model.TargetShiftEventId) : [];
    }

    private async Task<IList<SelectListItem>> ShiftOptionsAsync(string workerId, string selected)
    {
        if (string.IsNullOrEmpty(workerId))
        {
            return [];
        }

        var now = _clock.UtcNow;
        var options = new List<SelectListItem>();

        foreach (var shift in await _shiftService.ListWorkerShiftsAsync(workerId, now, now.AddDays(UpcomingDays), HttpContext.RequestAborted))
        {
            options.Add(new SelectListItem(Describe(shift), shift.ItemId, shift.ItemId == selected));
        }

        return options;
    }

    private static string Describe(CalendarEvent shift)
    {
        var start = CalendarTime.ToLocal(shift.StartUtc, shift.TimeZoneId ?? "UTC");
        var end = CalendarTime.ToLocal(shift.EndUtc, shift.TimeZoneId ?? "UTC");

        return $"{start:ddd d MMM HH:mm}-{end:HH:mm}";
    }
}
