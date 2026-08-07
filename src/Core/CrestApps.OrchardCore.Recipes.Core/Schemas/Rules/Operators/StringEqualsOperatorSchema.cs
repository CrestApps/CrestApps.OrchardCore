namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringEqualsOperator</c> condition operator.
/// </summary>
public sealed class StringEqualsOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringEqualsOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringEqualsOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Equals";

    /// <inheritdoc />
    protected override string Description => "Matches when the value equals the comparison text.";
}
