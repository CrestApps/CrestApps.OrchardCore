namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringEndsWithOperator</c> condition operator.
/// </summary>
public sealed class StringEndsWithOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringEndsWithOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringEndsWithOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Ends with";

    /// <inheritdoc />
    protected override string Description => "Matches when the value ends with the comparison text.";
}
