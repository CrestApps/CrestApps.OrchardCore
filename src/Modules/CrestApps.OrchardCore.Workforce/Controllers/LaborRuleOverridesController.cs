using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The labor rules gone ahead with, and the reasons given.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
public sealed class LaborRuleOverridesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILaborRuleOverrideService _overrideService;
    private readonly ILaborRuleSetManager _setManager;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRuleOverridesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="overrideService">The override service.</param>
    /// <param name="setManager">The rule set manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="siteService">The site service.</param>
    public LaborRuleOverridesController(
        IAuthorizationService authorizationService,
        ILaborRuleOverrideService overrideService,
        ILaborRuleSetManager setManager,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IWorkforceScopeService scopeService,
        ISiteService siteService)
    {
        _authorizationService = authorizationService;
        _overrideService = overrideService;
        _setManager = setManager;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _scopeService = scopeService;
        _siteService = siteService;
    }

    /// <summary>
    /// Lists the overrides, newest first.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/rule-overrides", "WorkforceRuleOverrides")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageLaborRules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, overrides) = await _overrideService.PageAsync(scope.All ? null : scope.TeamIds.ToArray(), pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var rules = _setManager.ListRules().ToDictionary(rule => rule.Key, rule => rule.DisplayName, StringComparer.Ordinal);
        var rows = new List<LaborRuleOverrideRowViewModel>();
        var timeZoneId = await PlanningOptions.TimeZoneOfAsync(null, _siteService);

        foreach (var item in overrides)
        {
            rows.Add(new LaborRuleOverrideRowViewModel
            {
                Override = item,
                WorkerName = (await _workerManager.FindByIdAsync(item.WorkerId))?.DisplayName,
                TeamName = (await _teamManager.FindByIdAsync(item.TeamId))?.Name,
                RuleName = rules.TryGetValue(item.RuleKey ?? string.Empty, out var name) ? name : item.RuleKey,
                RuleSetName = string.IsNullOrEmpty(item.RuleSetId) ? null : (await _setManager.FindByIdAsync(item.RuleSetId))?.Name,
                TimeZoneId = timeZoneId,
            });
        }

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<LaborRuleOverrideRowViewModel>
            {
                Source = rows,
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }
}
