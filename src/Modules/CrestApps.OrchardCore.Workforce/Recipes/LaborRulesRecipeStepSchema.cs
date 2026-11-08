using System.Reflection;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.Recipes.WorkforceSchemaBuilder;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the WorkforceLaborRules recipe step to CrestApps Recipes.
/// </summary>
internal sealed class LaborRulesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => LaborRulesRecipeStep.LaborRulesStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= Step(
            LaborRulesRecipeStep.LaborRulesStepName,
            "Creates or updates labor rule sets, matched by itemId then name. A rule set that is updated takes every member of the entry.",
            ("RuleSets", Array(RuleSet(), "The labor rule sets to create or update.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder RuleSet()
        => Object(
            "A labor rule set.",
            ("itemId", Text("The set's id. When it matches a stored set, that set is updated; otherwise it is matched by name.")),
            ("name", Text("The set name, unique.")),
            ("description", Text("What the set is for.")),
            ("jurisdiction", Text("The place or law it follows, as free text.")),
            ("teamNames", Strings("The teams it applies to (with their sub-teams), by name; added to teamIds. A name that doesn't match a team refuses the set.")),
            ("teamIds", Strings("The teams it applies to, by id.")),
            ("workerIds", Strings("The workers it applies to, by id.")),
            ("employmentTypes", EnumArray<EmploymentType>("The employment types it applies to; empty for all.")),
            ("enabled", Flag("Whether the set is in use. Defaults to true.")),
            ("rules", Array(Rule(), "The rules.")))
        .Required("name");

    private static JsonSchemaBuilder Rule()
        => Object(
            "A rule of the set.",
            ("id", Text("The rule's id within the set; one is made when omitted.")),
            ("ruleKey", RecipeStepSchemaBuilders.SuggestedString(typeof(LaborRuleKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()))
                .Description("The rule's key. These are the built-in rules; other modules add their own.")),
            ("enforcement", Enum<LaborRuleEnforcement>("What happens when a schedule breaks the rule. Defaults to Warn.")),
            ("parameters", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The rule's parameters by name, written invariantly as strings, such as { \"Hours\": \"10\" }.")))
        .Required("ruleKey");
}
