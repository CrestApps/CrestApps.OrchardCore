namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;

/// <summary>
/// Describes the recipe schema for the <c>ContentTypeCondition</c> rule condition.
/// </summary>
public sealed class ContentTypeConditionSchema : OperandConditionSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentTypeCondition";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Rules.Models.ContentTypeCondition, OrchardCore.Rules";

    /// <inheritdoc />
    protected override string DisplayText => "Content type";

    /// <inheritdoc />
    protected override string Description => "Evaluates the currently displayed content type against a value.";

    /// <inheritdoc />
    protected override string ValueDescription => "The content type value the operator compares the currently displayed content type against, for example Article.";

    /// <inheritdoc />
    protected override IEnumerable<string> GetValueExamples(RecipeSchemaExamples examples) => examples.ContentTypeNames;
}
