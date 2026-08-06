using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>CultureCondition</c> rule condition.
/// </summary>
public sealed class CultureConditionSchema : OperandConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CultureCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.CultureCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Culture";

    /// <inheritdoc />
    protected override string Description => "Evaluates the current UI culture against a value.";

    /// <inheritdoc />
    protected override string ValueDescription => "The culture value the operator compares the current UI culture against, for example en-US.";

    /// <inheritdoc />
    protected override IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => examples.CultureNames;
}
