using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>IsAuthenticatedCondition</c> rule condition.
/// </summary>
public sealed class IsAuthenticatedConditionSchema : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "IsAuthenticatedCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.IsAuthenticatedCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Is authenticated";

    /// <inheritdoc />
    protected override string Description => "Evaluates whether the current user is authenticated.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
        => [];
}
