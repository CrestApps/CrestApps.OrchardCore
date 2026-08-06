namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringContainsOperator</c> condition operator.
/// </summary>
public sealed class StringContainsOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringContainsOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringContainsOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Contains";

    /// <inheritdoc />
    protected override string Description => "Matches when the value contains the comparison text.";
}
