using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Models;
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
/// Adherence rule sets: which agent states each activity allows, and the grace and alert times.
/// </summary>
public sealed class AdherenceRuleSetsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AdherenceRuleSetManager _manager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceRuleSetsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The rule set manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AdherenceRuleSetsController(
        IAuthorizationService authorizationService,
        AdherenceRuleSetManager manager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<AdherenceRuleSetsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the rule sets.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence/rule-sets", "WorkforceAdherenceRuleSets")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new AdherenceExceptionListViewModel
        {
            List = new AdminListContext<AdherenceRuleSet>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The rule set, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/adherence/rule-sets/edit/{id?}", "WorkforceAdherenceRuleSetsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new AdherenceRuleSetEditViewModel
        {
            Id = id,
            Name = item.Name,
            TeamIds = [.. item.TeamIds],
            Rules = [.. Enum.GetValues<ActivityKind>().Select(kind =>
            {
                var rule = AdherenceCalculator.RuleFor(item.Rules, kind, null);

                return new AdherenceRuleEditViewModel
                {
                    Kind = kind,
                    AllowedStatuses = [.. rule.AllowedStatuses.Select(status => status.ToString())],
                    AllowedReasons = string.Join(", ", rule.AllowedReasons),
                    StartGraceMinutes = rule.StartGraceMinutes,
                    EndGraceMinutes = rule.EndGraceMinutes,
                    MinimumOutMinutes = rule.MinimumOutMinutes,
                    AlertAfterMinutes = rule.AlertAfterMinutes,
                };
            })],
        };

        model.TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, WorkforceScope.Everything, model.TeamIds, HttpContext.RequestAborted);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>The list, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/adherence/rule-sets/edit/{id?}", "WorkforceAdherenceRuleSetsEdit")]
    public async Task<IActionResult> EditPost(AdherenceRuleSetEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = model.Name?.Trim();
        item.TeamIds = [.. (model.TeamIds ?? []).Where(teamId => !string.IsNullOrEmpty(teamId)).Distinct(StringComparer.Ordinal)];
        item.Rules = [.. (model.Rules ?? []).Select(rule => new AdherenceRule
        {
            Kind = rule.Kind,
            AllowedStatuses = [.. (rule.AllowedStatuses ?? []).Select(status => Enum.TryParse<AgentPresenceStatus>(status, out var parsed) ? parsed : (AgentPresenceStatus?)null).Where(status => status.HasValue).Select(status => status.Value).Distinct()],
            AllowedReasons = [.. (rule.AllowedReasons ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            StartGraceMinutes = rule.StartGraceMinutes,
            EndGraceMinutes = rule.EndGraceMinutes,
            MinimumOutMinutes = rule.MinimumOutMinutes,
            AlertAfterMinutes = rule.AlertAfterMinutes,
        })];

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            model.TeamOptions = await WorkforceTeamOptions.ListAsync(_teamManager, WorkforceScope.Everything, model.TeamIds, HttpContext.RequestAborted);

            return View(model);
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The rule set was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a rule set.
    /// </summary>
    /// <param name="id">The rule set.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("workforce/adherence/rule-sets/delete/{id}", "WorkforceAdherenceRuleSetsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageAdherence))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        await _manager.DeleteAsync(item);
        await _notifier.SuccessAsync(H["The rule set was deleted."]);

        return RedirectToAction(nameof(Index));
    }
}
