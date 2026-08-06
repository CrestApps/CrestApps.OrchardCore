using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Provides the shared schema surface for conditions that compare a runtime value against a configured value
/// using a string operator, such as <c>UrlCondition</c>, <c>RoleCondition</c>, <c>CultureCondition</c> and
/// <c>ContentTypeCondition</c>.
/// </summary>
public abstract class OperandConditionSchemaDefinitionBase : RuleConditionSchemaDefinitionBase
{
    /// <summary>
    /// Gets the description shown for the <c>Value</c> property.
    /// </summary>
    protected virtual string ValueDescription => "The value the operator compares the current request against.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Value", RuleConditionSchemaBuilders.String(ValueDescription));
        yield return ("Operation", context.OperatorSchema);
    }
}
