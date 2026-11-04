using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Teams.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Teams.Controllers;

/// <summary>
/// The team hierarchy: the list, the editor and deleting a team.
/// </summary>
public sealed class TeamsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITeamManager _teamManager;
    private readonly IDisplayManager<Team> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="displayManager">The display manager of teams, which builds the rows and the editor.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public TeamsController(
        IAuthorizationService authorizationService,
        ITeamManager teamManager,
        IDisplayManager<Team> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IHtmlLocalizer<TeamsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _teamManager = teamManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the teams, each followed by its sub-teams.
    /// </summary>
    /// <param name="search">The text the name must contain.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("teams", "TeamsIndex")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var tree = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => item.Team)
            .Where(team => string.IsNullOrWhiteSpace(search) || (team.Name ?? string.Empty).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new TeamsListViewModel
        {
            Search = search,
            List = new AdminListContext<Team>
            {
                Source = tree.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, tree.Count, new RouteValueDictionary { { "search", search } }),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new team.
    /// </summary>
    /// <param name="parentId">The team the new one goes under, or empty for the top level.</param>
    /// <returns>The editor.</returns>
    [Admin("teams/create", "TeamsCreate")]
    public async Task<IActionResult> Create(string parentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.NewAsync(cancellationToken: HttpContext.RequestAborted);

        team.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;

        return View(new EditCatalogEntryViewModel
        {
            Editor = await _displayManager.BuildEditorAsync(team, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates a team.
    /// </summary>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("teams/create", "TeamsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.NewAsync(cancellationToken: HttpContext.RequestAborted);
        var editor = await _displayManager.UpdateEditorAsync(team, _updateModelAccessor.ModelUpdater, isNew: true);
        var isValid = await CatalogEntryValidation.ValidateAsync(_teamManager, team, _updateModelAccessor.ModelUpdater, nameof(Team), HttpContext.RequestAborted);

        if (!isValid || !ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel
            {
                Editor = editor,
            });
        }

        await _teamManager.CreateAsync(team, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The team has been created."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows the editor of a team.
    /// </summary>
    /// <param name="id">The team id.</param>
    /// <returns>The editor.</returns>
    [Admin("teams/edit/{id}", "TeamsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.FindByIdAsync(id, HttpContext.RequestAborted);

        if (team is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = team.Name,
            Editor = await _displayManager.BuildEditorAsync(team, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a team.
    /// </summary>
    /// <param name="id">The team id.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("teams/edit/{id}", "TeamsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.FindByIdAsync(id, HttpContext.RequestAborted);

        if (team is null)
        {
            return NotFound();
        }

        var displayName = team.Name;
        var editor = await _displayManager.UpdateEditorAsync(team, _updateModelAccessor.ModelUpdater, isNew: false);
        var isValid = await CatalogEntryValidation.ValidateAsync(_teamManager, team, _updateModelAccessor.ModelUpdater, nameof(Team), HttpContext.RequestAborted);

        if (!isValid || !ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel
            {
                DisplayName = displayName,
                Editor = editor,
            });
        }

        await _teamManager.UpdateAsync(team, cancellationToken: HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The team has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a team that has no sub-teams and that no module still holds, such as Workforce while it has members.
    /// </summary>
    /// <param name="id">The team id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("teams/delete/{id}", "TeamsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TeamsPermissions.ManageTeams))
        {
            return Forbid();
        }

        var team = await _teamManager.FindByIdAsync(id, HttpContext.RequestAborted);

        if (team is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var blockers = await _teamManager.GetDeletionBlockersAsync(team, HttpContext.RequestAborted);

        if (blockers.Count > 0)
        {
            await _notifier.ErrorAsync(H["The team can't be deleted. {0}", string.Join(" ", blockers)]);

            return RedirectToAction(nameof(Index));
        }

        await _teamManager.DeleteAsync(team, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The team has been deleted."]);

        return RedirectToAction(nameof(Index));
    }
}
