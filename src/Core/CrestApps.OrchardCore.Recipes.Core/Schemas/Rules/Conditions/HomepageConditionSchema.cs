using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>HomepageCondition</c> rule condition.
/// </summary>
public sealed class HomepageConditionSchema : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "HomepageCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.HomepageCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Homepage";

    /// <inheritdoc />
    protected override string Description => "Evaluates whether the current page is the site homepage.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
    {
        yield return ("Value", RuleConditionSchemaBuilders.Boolean("When true, matches the homepage. When false, matches any page that is not the homepage. Defaults to true."));
    }
}
