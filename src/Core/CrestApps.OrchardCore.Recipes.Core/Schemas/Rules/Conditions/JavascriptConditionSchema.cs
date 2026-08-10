using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>JavascriptCondition</c> rule condition.
/// </summary>
public sealed class JavascriptConditionSchema : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "JavascriptCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.JavascriptCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Javascript";

    /// <inheritdoc />
    protected override string Description => "A script condition written in JavaScript. The script must return true or false.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Script", RuleConditionSchemaBuilders.String("The JavaScript to evaluate. The script must return true or false, for example isHomepage()."));
    }
}
