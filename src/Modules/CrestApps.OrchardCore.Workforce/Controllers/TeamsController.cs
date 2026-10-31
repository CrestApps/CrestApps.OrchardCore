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
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The team hierarchy: the list and the editor.
/// </summary>
public sealed class TeamsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly IClock _clock;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TeamsController(
        IAuthorizationService authorizationService,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IClock clock,
        IShellFeaturesManager shellFeaturesManager,
        INotifier notifier,
        IHtmlLocalizer<TeamsController> htmlLocalizer,
        IStringLocalizer<TeamsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _clock = clock;
        _shellFeaturesManager = shellFeaturesManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the teams, each followed by its sub-teams.
    /// </summary>
    /// <param name="search">The text the name must contain.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/teams", "WorkforceTeams")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTeams))
        {
            return Forbid();
        }

        var tree = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => item.Team)
            .Where(team => string.IsNullOrWhiteSpace(search) || team.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            Search = search,
            List = new AdminListContext<WorkforceTeam>
            {
                Source = tree.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, tree.Count, new RouteValueDictionary { { "search", search } }),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing team.
    /// </summary>
    /// <param name="id">The team id, or empty for a new team.</param>
    /// <param name="parentId">The parent of a new team.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/teams/edit/{id?}", "WorkforceTeamsEdit")]
    public async Task<IActionResult> Edit(string id, string parentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = string.IsNullOrEmpty(id)
            ? await _teamManager.NewAsync()
            : await _teamManager.FindByIdAsync(id);

        if (team is null)
        {
            return NotFound();
        }

        var model = new TeamEditViewModel
        {
            Id = id,
            Name = team.Name,
            Kind = team.Kind.ToString(),
            ParentId = string.IsNullOrEmpty(id) ? parentId : team.ParentId,
            Description = team.Description,
            LinkedRoleName = team.LinkedRoleName,
            OpenShiftClaimPolicy = team.OpenShiftClaimPolicy?.ToString(),
            ShiftTradesNeedApproval = team.ShiftTradesNeedApproval.HasValue ? (team.ShiftTradesNeedApproval.Value ? "true" : "false") : null,
            TimeZoneId = team.TimeZoneId,
            Address = team.Location?.Address,
            Enabled = team.Enabled,
        };

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a team.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/teams/edit/{id?}", "WorkforceTeamsEdit")]
    public async Task<IActionResult> EditPost(TeamEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTeams))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var team = isNew
            ? await _teamManager.NewAsync()
            : await _teamManager.FindByIdAsync(model.Id);

        if (team is null)
        {
            return NotFound();
        }

        team.Name = model.Name;
        team.Kind = Enum.TryParse<TeamKind>(model.Kind, out var kind) ? kind : TeamKind.Team;
        team.ParentId = model.ParentId;
        team.Description = model.Description;
        team.LinkedRoleName = model.LinkedRoleName;
        team.TimeZoneId = model.TimeZoneId;

        if (await IsShiftPlanningOnAsync())
        {
            team.OpenShiftClaimPolicy = Enum.TryParse<OpenShiftClaimPolicy>(model.OpenShiftClaimPolicy, out var policy) ? policy : null;
            team.ShiftTradesNeedApproval = bool.TryParse(model.ShiftTradesNeedApproval, out var needsApproval) ? needsApproval : null;
        }
        team.Location = string.IsNullOrWhiteSpace(model.Address) ? null : new WorkforceLocation { Address = model.Address.Trim(), Latitude = team.Location?.Latitude, Longitude = team.Location?.Longitude };
        team.Enabled = model.Enabled;

        var validation = await _teamManager.ValidateAsync(team);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await FillAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _teamManager.CreateAsync(team);
        }
        else
        {
            await _teamManager.UpdateAsync(team);
        }

        await _notifier.SuccessAsync(H["The team has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a team that has no sub-teams and no current members. Its calendar is kept.
    /// </summary>
    /// <param name="id">The team id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/teams/delete/{id}", "WorkforceTeamsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.FindByIdAsync(id);

        if (team is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var subtree = await _teamManager.GetSubtreeIdsAsync(id, HttpContext.RequestAborted);
        var members = await _workerManager.ListByTeamAsync(id, DateOnly.FromDateTime(_clock.UtcNow), HttpContext.RequestAborted);

        if (subtree.Count > 1 || members.Count > 0)
        {
            await _notifier.ErrorAsync(H["The team still has sub-teams or members. Move them first."]);

            return RedirectToAction(nameof(Index));
        }

        await _teamManager.DeleteAsync(team);
        await _notifier.SuccessAsync(H["The team has been deleted. Its calendar is kept."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(TeamEditViewModel model)
    {
        var tree = await _teamManager.ListTreeAsync(HttpContext.RequestAborted);
        var excluded = string.IsNullOrEmpty(model.Id)
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await _teamManager.GetSubtreeIdsAsync(model.Id, HttpContext.RequestAborted)).ToHashSet(StringComparer.Ordinal);

        model.ParentOptions = tree
            .Where(item => !excluded.Contains(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == model.ParentId))
            .ToList();
        model.KindOptions = TeamKindOptions(S, model.Kind);
        model.TimeZoneOptions = _clock.GetTimeZones().Select(zone => new SelectListItem(zone.TimeZoneId, zone.TimeZoneId, zone.TimeZoneId == model.TimeZoneId)).ToList();
        model.ShowShiftPlanning = await IsShiftPlanningOnAsync();
    }

    private async Task<bool> IsShiftPlanningOnAsync()
    {
        return (await _shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == WorkforceConstants.Features.ShiftPlanning);
    }

    internal static IList<SelectListItem> TeamKindOptions(IStringLocalizer S, string selected)
    {
        return
        [
            new SelectListItem(S["Organization"], nameof(TeamKind.Organization), selected == nameof(TeamKind.Organization)),
            new SelectListItem(S["Region"], nameof(TeamKind.Region), selected == nameof(TeamKind.Region)),
            new SelectListItem(S["Site"], nameof(TeamKind.Site), selected == nameof(TeamKind.Site)),
            new SelectListItem(S["Department"], nameof(TeamKind.Department), selected == nameof(TeamKind.Department)),
            new SelectListItem(S["Team"], nameof(TeamKind.Team), selected == nameof(TeamKind.Team) || string.IsNullOrEmpty(selected)),
            new SelectListItem(S["Crew"], nameof(TeamKind.Crew), selected == nameof(TeamKind.Crew)),
        ];
    }
}
