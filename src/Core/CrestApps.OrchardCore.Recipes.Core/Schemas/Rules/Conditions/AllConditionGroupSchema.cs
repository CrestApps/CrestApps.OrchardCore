namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>AllConditionGroup</c> rule condition.
/// </summary>
public sealed class AllConditionGroupSchema : ConditionGroupSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "AllConditionGroup";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.AllConditionGroup, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "All conditions";

    /// <inheritdoc />
    protected override string Description => "A condition group that requires every nested condition to be true.";
}
