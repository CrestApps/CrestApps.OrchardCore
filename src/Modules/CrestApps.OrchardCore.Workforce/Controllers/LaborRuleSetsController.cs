using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The labor rule sets: the list and the editor, where rules are added from the catalog with their parameters.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
public sealed class LaborRuleSetsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILaborRuleSetManager _setManager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRuleSetsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="setManager">The rule set manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LaborRuleSetsController(
        IAuthorizationService authorizationService,
        ILaborRuleSetManager setManager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<LaborRuleSetsController> htmlLocalizer,
        IStringLocalizer<LaborRuleSetsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _setManager = setManager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the rule sets.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/labor-rules", "WorkforceLaborRules")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageLaborRules))
        {
            return Forbid();
        }

        var sets = (await _setManager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(set => set.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<LaborRuleSet>
            {
                Source = sets.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, sets.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing rule set.
    /// </summary>
    /// <param name="id">The set id, or empty for a new set.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/labor-rules/edit/{id?}", "WorkforceLaborRulesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageLaborRules))
        {
            return Forbid();
        }

        var set = string.IsNullOrEmpty(id) ? await _setManager.NewAsync() : await _setManager.FindByIdAsync(id);

        if (set is null)
        {
            return NotFound();
        }

        var model = new LaborRuleSetEditViewModel
        {
            Id = id,
            Name = set.Name,
            Description = set.Description,
            Jurisdiction = set.Jurisdiction,
            TeamIds = set.TeamIds.ToArray(),
            EmploymentTypes = set.EmploymentTypes.Select(type => type.ToString()).ToArray(),
            Enabled = set.Enabled,
            Rules = set.Rules.Select(rule => new LaborRuleRowEditViewModel
            {
                Id = rule.Id,
                RuleKey = rule.RuleKey,
                Enforcement = rule.Enforcement.ToString(),
                Parameters = new Dictionary<string, string>(rule.Parameters, StringComparer.Ordinal),
            }).ToList(),
            WorkerCount = set.WorkerIds.Count,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a rule set, or adds or removes a rule and shows the editor again.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <param name="addRule">Whether to add the chosen rule.</param>
    /// <param name="removeRule">The id of a rule to remove.</param>
    /// <returns>A redirect to the list, or the editor.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/labor-rules/edit/{id?}", "WorkforceLaborRulesEdit")]
    public async Task<IActionResult> EditPost(LaborRuleSetEditViewModel model, bool addRule = false, string removeRule = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageLaborRules))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var set = isNew ? await _setManager.NewAsync() : await _setManager.FindByIdAsync(model.Id);

        if (set is null)
        {
            return NotFound();
        }

        model.Rules ??= [];

        if (addRule || !string.IsNullOrEmpty(removeRule))
        {
            ModelState.Clear();

            if (addRule && _setManager.NewRule(model.AddRuleKey) is { } added)
            {
                model.Rules.Add(new LaborRuleRowEditViewModel
                {
                    Id = added.Id,
                    RuleKey = added.RuleKey,
                    Enforcement = added.Enforcement.ToString(),
                    Parameters = new Dictionary<string, string>(added.Parameters, StringComparer.Ordinal),
                });
            }

            if (!string.IsNullOrEmpty(removeRule))
            {
                model.Rules = model.Rules.Where(rule => rule.Id != removeRule).ToList();
            }

            model.AddRuleKey = null;
            model.WorkerCount = set.WorkerIds.Count;
            await PopulateAsync(model);

            return View(model);
        }

        set.Name = model.Name;
        set.Description = model.Description?.Trim();
        set.Jurisdiction = model.Jurisdiction?.Trim();
        set.TeamIds = (model.TeamIds ?? []).Where(teamId => !string.IsNullOrEmpty(teamId)).Distinct(StringComparer.Ordinal).ToList();
        set.EmploymentTypes = (model.EmploymentTypes ?? []).Select(type => Enum.TryParse<EmploymentType>(type, out var parsed) ? parsed : (EmploymentType?)null).Where(type => type.HasValue).Select(type => type.Value).Distinct().ToList();
        set.Enabled = model.Enabled;
        set.Rules = model.Rules.Select(rule => new LaborRuleInstance
        {
            Id = rule.Id,
            RuleKey = rule.RuleKey,
            Enforcement = Enum.TryParse<LaborRuleEnforcement>(rule.Enforcement, out var enforcement) ? enforcement : LaborRuleEnforcement.Warn,
            Parameters = (rule.Parameters ?? []).Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.Ordinal),
        }).ToList();

        var validation = await _setManager.ValidateAsync(set);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            model.WorkerCount = set.WorkerIds.Count;
            await PopulateAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _setManager.CreateAsync(set);
        }
        else
        {
            await _setManager.UpdateAsync(set);
        }

        await _notifier.SuccessAsync(H["The labor rule set has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a rule set.
    /// </summary>
    /// <param name="id">The set id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/labor-rules/delete/{id}", "WorkforceLaborRulesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageLaborRules))
        {
            return Forbid();
        }

        var set = await _setManager.FindByIdAsync(id);

        if (set is not null)
        {
            await _setManager.DeleteAsync(set);
            await _notifier.SuccessAsync(H["The labor rule set has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(LaborRuleSetEditViewModel model)
    {
        var teams = new HashSet<string>(model.TeamIds ?? [], StringComparer.Ordinal);
        var types = new HashSet<string>(model.EmploymentTypes ?? [], StringComparer.Ordinal);
        var rules = _setManager.ListRules();

        model.Catalog = rules.ToDictionary(rule => rule.Key, StringComparer.Ordinal);
        model.RuleOptions = rules.Select(rule => new SelectListItem(rule.DisplayName, rule.Key)).ToList();
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, teams.Contains(item.Team.ItemId)))
            .ToList();
        model.EmploymentOptions =
        [
            new SelectListItem(S["Full time"], nameof(EmploymentType.FullTime), types.Contains(nameof(EmploymentType.FullTime))),
            new SelectListItem(S["Part time"], nameof(EmploymentType.PartTime), types.Contains(nameof(EmploymentType.PartTime))),
            new SelectListItem(S["Seasonal"], nameof(EmploymentType.Seasonal), types.Contains(nameof(EmploymentType.Seasonal))),
            new SelectListItem(S["Contractor"], nameof(EmploymentType.Contractor), types.Contains(nameof(EmploymentType.Contractor))),
            new SelectListItem(S["Temporary"], nameof(EmploymentType.Temporary), types.Contains(nameof(EmploymentType.Temporary))),
            new SelectListItem(S["Subcontractor"], nameof(EmploymentType.Subcontractor), types.Contains(nameof(EmploymentType.Subcontractor))),
        ];
    }
}
