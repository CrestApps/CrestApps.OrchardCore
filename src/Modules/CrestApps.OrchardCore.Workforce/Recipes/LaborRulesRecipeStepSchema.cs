using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

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
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(LaborRulesRecipeStep.LaborRulesStepName).Description("Recipe step discriminator. Must be 'WorkforceLaborRules'.")),
                ("RuleSets", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(RuleSet()).Description("The labor rule sets to create, or update by id or name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder RuleSet()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The set id; omit to match by name.")),
                ("name", Text("The set name.")),
                ("description", Text("What the set is for.")),
                ("jurisdiction", Text("The place or law it follows, as free text.")),
                ("teamNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The names of the teams it applies to, with their sub-teams.")),
                ("employmentTypes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("FullTime", "PartTime", "Seasonal", "Temporary", "Contractor", "Subcontractor")).Description("The employment types it applies to; empty for all.")),
                ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the set is in use.")),
                ("rules", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Rule()).Description("The rules.")))
            .Required("name")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Rule()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("id", Text("The rule's id within the set; one is made when omitted.")),
                ("ruleKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(
                    "MaxHoursPerDay", "MaxHoursPerWeek", "MinHoursPerWeek", "MinRest", "MaxConsecutiveDays", "DaysOffPerWeek", "MaxShiftLength", "MinShiftLength",
                    "Breaks", "MinorLimits", "SkillsRequired", "NoWorkDuringTimeOff", "ScheduleNotice", "Overtime", "PredictabilityPay").Description("The rule's key; other modules add their own.")),
                ("enforcement", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Warn", "Block", "RequireOverride")),
                ("parameters", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The rule's parameters by name, written invariantly, such as { \"Hours\": \"10\" }.")))
            .Required("ruleKey")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Text(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
    }
}
