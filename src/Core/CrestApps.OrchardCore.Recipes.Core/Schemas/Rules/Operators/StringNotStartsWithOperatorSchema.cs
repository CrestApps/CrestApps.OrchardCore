namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringNotStartsWithOperator</c> condition operator.
/// </summary>
public sealed class StringNotStartsWithOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringNotStartsWithOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringNotStartsWithOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Does not start with";

    /// <inheritdoc />
    protected override string Description => "Matches when the value does not start with the comparison text.";
}
