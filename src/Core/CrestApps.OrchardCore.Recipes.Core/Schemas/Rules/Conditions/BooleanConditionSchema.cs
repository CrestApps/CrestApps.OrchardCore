using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>BooleanCondition</c> rule condition.
/// </summary>
public sealed class BooleanConditionSchema : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "BooleanCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.BooleanCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Boolean";

    /// <inheritdoc />
    protected override string Description => "A boolean condition that evaluates to either true or false.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Value", RuleConditionSchemaBuilders.Boolean("Whether the condition evaluates to true or false. Defaults to true."));
    }
}
