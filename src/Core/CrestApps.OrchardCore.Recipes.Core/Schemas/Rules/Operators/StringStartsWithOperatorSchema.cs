namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;

/// <summary>
/// Describes the recipe schema for the <c>StringStartsWithOperator</c> condition operator.
/// </summary>
public sealed class StringStartsWithOperatorSchema : StringOperatorSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "StringStartsWithOperator";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Starts with";

    /// <inheritdoc />
    protected override string Description => "Matches when the value starts with the comparison text.";
}
