using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// The contact center agents and their workers, with a way to give every agent a worker.
/// </summary>
public sealed class AgentsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AgentWorkerService _agentWorkerService;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentWorkerService">The agent and worker link.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AgentsController(
        IAuthorizationService authorizationService,
        AgentWorkerService agentWorkerService,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<AgentsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _agentWorkerService = agentWorkerService;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the agents.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/agents", "WorkforceAgents")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var rows = (await _agentWorkerService.ListAsync(HttpContext.RequestAborted))
            .Select(entry => new AgentWorkerRow { Agent = entry.Agent, Worker = entry.Worker })
            .OrderBy(row => row.Worker is not null)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new AgentWorkerListViewModel
        {
            Missing = rows.Count(row => row.Worker is null),
            TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, WorkforceScope.Everything, null, HttpContext.RequestAborted),
            List = new AdminListContext<AgentWorkerRow>
            {
                Source = rows.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, rows.Count),
            },
        });
    }

    /// <summary>
    /// Gives every agent without a worker one.
    /// </summary>
    /// <param name="teamId">The team the new workers join.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/agents/create-workers", "WorkforceAgentsCreateWorkers")]
    public async Task<IActionResult> CreateWorkers(string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageWorkers))
        {
            return Forbid();
        }

        var created = await _agentWorkerService.CreateMissingAsync(string.IsNullOrEmpty(teamId) ? null : teamId, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(created == 1 ? H["Created 1 worker."] : H["Created {0} workers.", created]);

        return RedirectToAction(nameof(Index));
    }
}
