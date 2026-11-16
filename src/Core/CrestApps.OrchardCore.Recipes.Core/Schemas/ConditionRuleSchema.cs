using CrestApps.OrchardCore.Conditions;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Describes a <see cref="ConditionRule"/> in a recipe schema, the same way wherever a rule travels (a checklist
/// question, a content field's settings).
/// </summary>
public static class ConditionRuleSchema
{
    /// <summary>
    /// Builds the schema of a rule.
    /// </summary>
    /// <param name="description">What the rule decides.</param>
    /// <param name="sourceDescription">What a condition's source names in this place.</param>
    /// <returns>The schema, which also accepts <see langword="null"/> for "always".</returns>
    public static JsonSchemaBuilder Build(string description, string sourceDescription)
    {
        var condition = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("One test of the rule.")
            .Properties(
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description(sourceDescription)),
                ("Operator", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(Enum.GetNames<ConditionOperator>()).Description("How the source's values are compared.")),
                ("Values", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The values compared, in the invariant culture. IsOneOf and IsNoneOf use them all, IsEmpty and IsNotEmpty none, the others the first.")))
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Description(description)
            .Properties(
                ("Match", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(Enum.GetNames<ConditionMatch>()).Description("All: every condition must be met. Any: one is enough.")),
                ("Conditions", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(condition).Description("The conditions. A rule without any always applies.")))
            .AdditionalProperties(true);
    }
}
