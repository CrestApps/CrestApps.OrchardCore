using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>RoleCondition</c> rule condition.
/// </summary>
public sealed class RoleConditionSchema : OperandConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "RoleCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.RoleCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Role";

    /// <inheritdoc />
    protected override string Description => "Evaluates the current user's roles against a value.";

    /// <inheritdoc />
    protected override string ValueDescription => "The role value the operator compares the current user's roles against, for example Administrator.";

    /// <inheritdoc />
    protected override IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => examples.RoleNames;
}
