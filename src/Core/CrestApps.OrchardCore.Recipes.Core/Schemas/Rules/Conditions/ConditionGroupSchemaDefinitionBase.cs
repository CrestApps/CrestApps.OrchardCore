using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Provides the shared schema surface for condition groups that nest other conditions, such as
/// <c>AllConditionGroup</c> and <c>AnyConditionGroup</c>.
/// </summary>
public abstract class ConditionGroupSchemaDefinitionBase : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    protected override bool IsGroup => true;

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("DisplayText", RuleConditionSchemaBuilders.NullableString("An optional label shown for the condition group in the rule editor."));
    }
}
