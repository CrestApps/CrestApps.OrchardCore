using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
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

    /// <summary>
    /// Gets the example values surfaced for the <c>Value</c> property. Override to supply live tenant values.
    /// </summary>
    /// <param name="examples">The example values available on the current tenant.</param>
    protected virtual IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => [];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Value", RuleConditionSchemaBuilders.String(ValueDescription).WithSuggestions(GetValueExamples(context.Examples)));
        yield return ("Operation", context.OperatorSchema);
    }
}
