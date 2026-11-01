using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// The WorkforceAdherence recipe step: adherence rule sets matched by id or name, with their teams by
/// <c>teamNames</c> and each rule's activity code by <c>activityName</c>.
/// </summary>
internal sealed class AdherenceRecipeStep : NamedRecipeStepHandler
{
    public const string AdherenceStepName = "WorkforceAdherence";

    private readonly AdherenceRuleSetManager _ruleSetManager;
    private readonly ITeamManager _teamManager;
    private readonly IActivityCodeManager _activityCodeManager;

    public AdherenceRecipeStep(AdherenceRuleSetManager ruleSetManager, ITeamManager teamManager, IActivityCodeManager activityCodeManager)
        : base(AdherenceStepName)
    {
        _ruleSetManager = ruleSetManager;
        _teamManager = teamManager;
        _activityCodeManager = activityCodeManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var import = new RecipeImport(context, AdherenceStepName);
        var teams = (await _teamManager.GetAllAsync()).ToList();
        var codes = (await _activityCodeManager.GetAllAsync()).ToList();
        var ruleSets = (await _ruleSetManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<AdherenceRuleSet>("RuleSets"))
        {
            var ok = true;

            foreach (var teamName in RecipeImport.Strings(node, "teamNames"))
            {
                var team = import.Named(teams, item => item.Name, teamName, "rule set", incoming.Name, "a team");

                ok &= team is not null;

                if (team is not null && !incoming.TeamIds.Contains(team.ItemId))
                {
                    incoming.TeamIds.Add(team.ItemId);
                }
            }

            var ruleNodes = (node["rules"] as JsonArray ?? []).OfType<JsonObject>().ToList();

            for (var i = 0; i < Math.Min(ruleNodes.Count, incoming.Rules.Count); i++)
            {
                if (RecipeImport.String(ruleNodes[i], "activityName") is { } activityName)
                {
                    var code = import.Named(codes, item => item.Name, activityName, "rule set", incoming.Name, "an activity code");

                    ok &= code is not null;
                    incoming.Rules[i].ActivityCodeId = code?.ItemId;
                }
            }

            if (ok && await import.SaveAsync(_ruleSetManager, incoming, RecipeImport.Existing(ruleSets, incoming, item => item.Name), $"rule set '{incoming.Name}'") is { } saved && !ruleSets.Contains(saved))
            {
                ruleSets.Add(saved);
            }
        }
    }
}
