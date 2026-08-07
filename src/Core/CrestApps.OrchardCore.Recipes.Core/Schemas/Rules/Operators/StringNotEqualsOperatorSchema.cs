namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringNotEqualsOperator</c> condition operator.
/// </summary>
public sealed class StringNotEqualsOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringNotEqualsOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringNotEqualsOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Does not equal";

    /// <inheritdoc />
    protected override string Description => "Matches when the value does not equal the comparison text.";
}
