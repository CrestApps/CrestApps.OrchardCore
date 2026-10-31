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
/// The coverage requirements: how many people each team needs, by weekday or date and time of day.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class CoverageController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICoverageService _coverageService;
    private readonly ITeamManager _teamManager;
    private readonly ISkillManager _skillManager;
    private readonly IWorkforceScopeService _scopeService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverageController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="coverageService">The coverage service.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="scopeService">The service that says which teams a user manages.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CoverageController(
        IAuthorizationService authorizationService,
        ICoverageService coverageService,
        ITeamManager teamManager,
        ISkillManager skillManager,
        IWorkforceScopeService scopeService,
        INotifier notifier,
        IHtmlLocalizer<CoverageController> htmlLocalizer,
        IStringLocalizer<CoverageController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _coverageService = coverageService;
        _teamManager = teamManager;
        _skillManager = skillManager;
        _scopeService = scopeService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the coverage requirements of a team the user manages.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/coverage", "WorkforceCoverage")]
    public async Task<IActionResult> Index(WorkforceListViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(model.TeamId) || !scope.Covers(model.TeamId))
        {
            model.TeamId = model.TeamOptions.FirstOrDefault()?.Value;

            foreach (var option in model.TeamOptions)
            {
                option.Selected = option.Value == model.TeamId;
            }
        }

        var requirements = (await _coverageService.ListRequirementsAsync(model.TeamId, HttpContext.RequestAborted))
            .OrderBy(requirement => requirement.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        model.List = new AdminListContext<CoverageRequirement>
        {
            Source = requirements.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, requirements.Count, new RouteValueDictionary { { "teamId", model.TeamId } }),
        };

        return View(model);
    }

    /// <summary>
    /// Shows the editor of a new or existing coverage requirement.
    /// </summary>
    /// <param name="id">The requirement id, or empty for a new requirement.</param>
    /// <param name="teamId">The team of a new requirement.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/coverage/edit/{id?}", "WorkforceCoverageEdit")]
    public async Task<IActionResult> Edit(string id, string teamId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var requirement = string.IsNullOrEmpty(id)
            ? new CoverageRequirement { TeamId = teamId }
            : await _coverageService.FindRequirementAsync(id, HttpContext.RequestAborted);

        if (requirement is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!string.IsNullOrEmpty(id) && !scope.Covers(requirement.TeamId))
        {
            return Forbid();
        }

        var model = new CoverageEditViewModel
        {
            Id = id,
            TeamId = requirement.TeamId,
            Name = requirement.Name,
            SkillId = requirement.SkillId,
            EffectiveFrom = requirement.EffectiveFrom,
            EffectiveTo = requirement.EffectiveTo,
            Rows = CoverageRowFormat.Write(requirement.Rows),
        };

        await PopulateAsync(model, scope);

        return View(model);
    }

    /// <summary>
    /// Saves a coverage requirement.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/coverage/edit/{id?}", "WorkforceCoverageEdit")]
    public async Task<IActionResult> EditPost(CoverageEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var requirement = isNew
            ? new CoverageRequirement()
            : await _coverageService.FindRequirementAsync(model.Id, HttpContext.RequestAborted);

        if (requirement is null)
        {
            return NotFound();
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if ((!isNew && !scope.Covers(requirement.TeamId)) || (!string.IsNullOrEmpty(model.TeamId) && !scope.Covers(model.TeamId)))
        {
            return Forbid();
        }

        var errors = new List<string>();
        var rows = CoverageRowFormat.Read(model.Rows, errors);

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(model.Rows), S["This row isn't understood: {0}", error]);
        }

        if (ModelState.IsValid)
        {
            requirement.TeamId = model.TeamId;
            requirement.Name = model.Name;
            requirement.SkillId = string.IsNullOrEmpty(model.SkillId) ? null : model.SkillId;
            requirement.EffectiveFrom = model.EffectiveFrom;
            requirement.EffectiveTo = model.EffectiveTo;
            requirement.Rows = rows;

            var result = await _coverageService.SaveRequirementAsync(requirement, HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, scope);

            return View(model);
        }

        await _notifier.SuccessAsync(H["The coverage requirement has been saved."]);

        return RedirectToAction(nameof(Index), new { teamId = model.TeamId });
    }

    /// <summary>
    /// Deletes a coverage requirement.
    /// </summary>
    /// <param name="id">The requirement id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/coverage/delete/{id}", "WorkforceCoverageDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageSchedules))
        {
            return Forbid();
        }

        var requirement = await _coverageService.FindRequirementAsync(id, HttpContext.RequestAborted);

        if (requirement is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var scope = await _scopeService.GetManagedScopeAsync(User, HttpContext.RequestAborted);

        if (!scope.Covers(requirement.TeamId))
        {
            return Forbid();
        }

        await _coverageService.DeleteRequirementAsync(id, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The coverage requirement has been deleted."]);

        return RedirectToAction(nameof(Index), new { teamId = requirement.TeamId });
    }

    private async Task PopulateAsync(CoverageEditViewModel model, WorkforceScope scope)
    {
        model.TeamOptions = await PlanningOptions.TeamsAsync(_teamManager, scope, model.TeamId, HttpContext.RequestAborted);
        model.SkillOptions = (await _skillManager.GetEnabledAsync(HttpContext.RequestAborted))
            .OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(skill => new SelectListItem(skill.Name, skill.ItemId, skill.ItemId == model.SkillId))
            .ToList();
    }
}
