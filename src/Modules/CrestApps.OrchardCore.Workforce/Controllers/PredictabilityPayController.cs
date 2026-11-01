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
/// The predictability pay of the teams the user manages: the list, and approving or waiving each entry.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
public sealed class PredictabilityPayController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IPredictabilityPayService _payService;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictabilityPayController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="payService">The predictability pay service.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PredictabilityPayController(
        IAuthorizationService authorizationService,
        IPredictabilityPayService payService,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        ISiteService siteService,
        INotifier notifier,
        IHtmlLocalizer<PredictabilityPayController> htmlLocalizer,
        IStringLocalizer<PredictabilityPayController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _payService = payService;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _siteService = siteService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the predictability pay of the teams the user manages, newest first.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/predictability-pay", "WorkforcePredictabilityPay")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApprovePredictabilityPay))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        IReadOnlyCollection<string> teamIds = !string.IsNullOrEmpty(model.TeamId)
            ? scope.Covers(model.TeamId) ? [model.TeamId] : []
            : scope.All ? null : scope.TeamIds.ToArray();
        PredictabilityPayStatus? status = string.IsNullOrEmpty(model.Status)
            ? PredictabilityPayStatus.Pending
            : Enum.TryParse<PredictabilityPayStatus>(model.Status, out var parsed) ? parsed : null;
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, entries) = await _payService.PageAsync(new PredictabilityPayQuery { TeamIds = teamIds, Status = status }, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rows = new List<PredictabilityPayRowViewModel>();

        foreach (var entry in entries)
        {
            var team = await _teamManager.FindByIdAsync(entry.TeamId);

            rows.Add(new PredictabilityPayRowViewModel
            {
                Entry = entry,
                WorkerName = (await _workerManager.FindByIdAsync(entry.WorkerId))?.DisplayName,
                TeamName = team?.Name,
                TimeZoneId = await PlanningOptions.TimeZoneOfAsync(team, _siteService),
                CanDecide = entry.Status == PredictabilityPayStatus.Pending,
            });
        }

        model.List = new AdminListContext<PredictabilityPayRowViewModel>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "teamId", model.TeamId }, { "status", model.Status } }),
        };
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.StatusOptions =
        [
            new SelectListItem(S["To decide"], string.Empty, string.IsNullOrEmpty(model.Status)),
            new SelectListItem(S["Approved"], nameof(PredictabilityPayStatus.Approved), status == PredictabilityPayStatus.Approved),
            new SelectListItem(S["Waived"], nameof(PredictabilityPayStatus.Waived), status == PredictabilityPayStatus.Waived),
            new SelectListItem(S["Exported"], nameof(PredictabilityPayStatus.Exported), status == PredictabilityPayStatus.Exported),
        ];

        return View(model);
    }

    /// <summary>
    /// Approves an entry to be paid.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/predictability-pay/approve/{id}", "WorkforcePredictabilityPayApprove")]
    public Task<IActionResult> Approve(string id)
    {
        return DecideAsync(id, () => _payService.ApproveAsync(id, User.Identity?.Name, HttpContext.RequestAborted), H["The predictability pay has been approved."]);
    }

    /// <summary>
    /// Waives an entry.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="reason">Why it isn't owed.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/predictability-pay/waive/{id}", "WorkforcePredictabilityPayWaive")]
    public Task<IActionResult> Waive(string id, string reason)
    {
        return DecideAsync(id, () => _payService.WaiveAsync(id, reason, User.Identity?.Name, HttpContext.RequestAborted), H["The predictability pay has been waived."]);
    }

    private async Task<IActionResult> DecideAsync(string id, Func<Task<PlanningResult<PredictabilityPayEntry>>> decide, LocalizedHtmlString success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ApprovePredictabilityPay))
        {
            return Forbid();
        }

        var entry = await _payService.FindAsync(id, HttpContext.RequestAborted);

        if (entry is null)
        {
            return NotFound();
        }

        if (!(await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted)).Covers(entry.TeamId))
        {
            return Forbid();
        }

        var result = await decide();

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Index));
    }
}
