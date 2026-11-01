using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Imports the WorkforceLaborRules step: labor rule sets matched by name, with their teams by <c>teamNames</c> and
/// their rules by key. Every set goes through its manager, so its rules are checked against the catalog.
/// </summary>
internal sealed class LaborRulesRecipeStep : NamedRecipeStepHandler
{
    public const string LaborRulesStepName = "WorkforceLaborRules";

    private readonly ILaborRuleSetManager _setManager;
    private readonly ITeamManager _teamManager;

    public LaborRulesRecipeStep(ILaborRuleSetManager setManager, ITeamManager teamManager)
        : base(LaborRulesStepName)
    {
        _setManager = setManager;
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var sets = (await _setManager.GetAllAsync()).ToList();
        var teams = await _teamManager.GetAllAsync();

        foreach (var node in (context.Step["RuleSets"] as JsonArray ?? []).OfType<JsonObject>())
        {
            LaborRuleSet incoming;

            try
            {
                incoming = node.Deserialize<LaborRuleSet>(WorkforceRecipeStep.JsonOptions);
            }
            catch (JsonException ex)
            {
                context.Errors.Add($"A rule set in the WorkforceLaborRules step can't be read: {ex.Message}");

                continue;
            }

            if (incoming is null)
            {
                continue;
            }

            var teamIds = new List<string>(incoming.TeamIds);
            var missing = false;

            foreach (var teamName in (node["teamNames"] as JsonArray ?? []).Select(item => item?.GetValue<string>()).Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                var team = teams.FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

                if (team is null)
                {
                    context.Errors.Add($"The rule set '{incoming.Name}' in the WorkforceLaborRules step names a team that doesn't exist: '{teamName}'.");
                    missing = true;

                    break;
                }

                teamIds.Add(team.ItemId);
            }

            if (missing)
            {
                continue;
            }

            var existing = sets.FirstOrDefault(set => set.ItemId == incoming.ItemId)
                ?? sets.FirstOrDefault(set => WorkforceNames.Normalize(set.Name) == WorkforceNames.Normalize(incoming.Name));
            var target = existing ?? await _setManager.NewAsync();

            target.Name = incoming.Name;
            target.Description = incoming.Description;
            target.Jurisdiction = incoming.Jurisdiction;
            target.Rules = incoming.Rules;
            target.TeamIds = teamIds.Distinct(StringComparer.Ordinal).ToList();
            target.WorkerIds = incoming.WorkerIds;
            target.EmploymentTypes = incoming.EmploymentTypes;
            target.Enabled = incoming.Enabled;

            var validation = await _setManager.ValidateAsync(target);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"The rule set '{incoming.Name}' in the WorkforceLaborRules step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (existing is null)
            {
                await _setManager.CreateAsync(target);
                sets.Add(target);
            }
            else
            {
                await _setManager.UpdateAsync(target);
            }
        }
    }
}
