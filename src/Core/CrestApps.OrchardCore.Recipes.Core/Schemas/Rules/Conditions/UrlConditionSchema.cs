namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>UrlCondition</c> rule condition.
/// </summary>
public sealed class UrlConditionSchema : OperandConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UrlCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Url";

    /// <inheritdoc />
    protected override string Description => "Evaluates the current request URL against a value.";

    /// <inheritdoc />
    protected override string ValueDescription => "The URL value the operator compares the current request URL against, for example /knowledge-base/category/.";
}
