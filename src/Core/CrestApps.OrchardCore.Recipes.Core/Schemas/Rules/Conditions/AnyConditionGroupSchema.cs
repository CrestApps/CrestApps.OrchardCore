namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>AnyConditionGroup</c> rule condition.
/// </summary>
public sealed class AnyConditionGroupSchema : ConditionGroupSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AnyConditionGroup";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.AnyConditionGroup, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Any conditions";

    /// <inheritdoc />
    protected override string Description => "A condition group that requires at least one nested condition to be true.";
}
