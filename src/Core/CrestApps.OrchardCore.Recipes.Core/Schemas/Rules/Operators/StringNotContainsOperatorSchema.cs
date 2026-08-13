namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringNotContainsOperator</c> condition operator.
/// </summary>
public sealed class StringNotContainsOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringNotContainsOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringNotContainsOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Does not contain";

    /// <inheritdoc />
    protected override string Description => "Matches when the value does not contain the comparison text.";
}
