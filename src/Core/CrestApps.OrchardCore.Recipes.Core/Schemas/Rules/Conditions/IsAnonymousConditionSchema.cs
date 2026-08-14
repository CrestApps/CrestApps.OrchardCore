using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>IsAnonymousCondition</c> rule condition.
/// </summary>
public sealed class IsAnonymousConditionSchema : RuleConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "IsAnonymousCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.IsAnonymousCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Is anonymous";

    /// <inheritdoc />
    protected override string Description => "Evaluates whether the current user is anonymous, meaning not authenticated.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RuleConditionSchemaContext context)
        => [];
}
