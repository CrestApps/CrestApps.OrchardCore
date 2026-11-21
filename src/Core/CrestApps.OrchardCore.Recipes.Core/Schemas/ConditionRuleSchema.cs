using CrestApps.OrchardCore.Conditions;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Describes a <see cref="ConditionRule"/> in a recipe schema, the same way wherever a rule travels (a checklist
/// question, a content field's settings, a survey question).
/// </summary>
public static class ConditionRuleSchema
{
    // Nested groups are described this many levels deep; deeper groups are accepted as objects without a described shape.
    private const int DescribedGroupDepth = 4;

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
                ("Values", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The values compared, in the invariant culture. IsOneOf and IsNoneOf use them all, Between the first two (in either order), IsEmpty and IsNotEmpty none, the others the first. Numbers and ISO 8601 dates compare by value.")))
            .AdditionalProperties(true);

        return Rule(description, condition, DescribedGroupDepth, nullable: true);
    }

    private static JsonSchemaBuilder Rule(string description, JsonSchemaBuilder condition, int depth, bool nullable)
    {
        var group = depth > 0
            ? Rule("A nested group of conditions, counted as one item of its parent rule.", condition, depth - 1, nullable: false)
            : new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("A nested group of conditions, counted as one item of its parent rule.");

        return new JsonSchemaBuilder()
            .Type(nullable ? SchemaValueType.Object | SchemaValueType.Null : SchemaValueType.Object)
            .Description(description)
            .Properties(
                ("Match", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(Enum.GetNames<ConditionMatch>()).Description("All: every condition and group must be met. Any: one is enough.")),
                ("Conditions", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(condition).Description("The conditions. A rule without any, in itself or its groups, always applies.")),
                ("Groups", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(group).Description("Nested groups, such as (A or B) in C and (A or B). Each counts as one item, met when its own rule is met.")))
            .AdditionalProperties(true);
    }
}
