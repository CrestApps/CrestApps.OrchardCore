using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.ContactCenter.Recipes.WorkforceContactCenterSchemaBuilder;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Describes the WorkforceAdherence recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AdherenceRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AdherenceRecipeStep.AdherenceStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var rule = Object(
            "A rule: which agent states count as adherent during an activity.",
            ("kind", Enum<ActivityKind>("The kind of scheduled activity the rule is for.")),
            ("activityName", Text("The activity code the rule is for, by name. Wins over activityCodeId.")),
            ("activityCodeId", Text("The activity code the rule is for, by id; null for every code of the kind.")),
            ("allowedStatuses", EnumArray<AgentPresenceStatus>("The agent states that count as adherent.")),
            ("allowedReasons", Strings("The state reasons the agent must have; empty for any reason.")),
            ("startGraceMinutes", Integer("How many minutes after the activity starts the agent may still be in the previous activity. Defaults to 2.")),
            ("endGraceMinutes", Integer("How many minutes before the activity ends the agent may move to the next one. Defaults to 2.")),
            ("minimumOutMinutes", Integer("The shortest time out of adherence that counts, in minutes. Defaults to 1.")),
            ("alertAfterMinutes", Integer("How many minutes out of adherence raise an alert. Defaults to 5.")));

        var ruleSet = Object(
            "An adherence rule set.",
            ("itemId", ItemId("rule set")),
            ("name", Text("The rule set name, unique.")),
            ("teamNames", Strings("The teams the rules apply to, by name; added to teamIds. Teams named must exist.")),
            ("teamIds", Strings("The teams the rules apply to, by id.")),
            ("rules", Array(rule, "The rules.")))
            .Required("name");

        _cached ??= Step(
            AdherenceRecipeStep.AdherenceStepName,
            "Creates or updates adherence rule sets, matched by itemId then name. A rule set that is updated takes every member of the entry.",
            ("RuleSets", Array(ruleSet, "The adherence rule sets to create or update.")));

        return ValueTask.FromResult(_cached);
    }
}
