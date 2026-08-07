namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringNotEndsWithOperator</c> condition operator.
/// </summary>
public sealed class StringNotEndsWithOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringNotEndsWithOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringNotEndsWithOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Does not end with";

    /// <inheritdoc />
    protected override string Description => "Matches when the value does not end with the comparison text.";
}
