namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContentDefinition" recipe step.
/// Composes part and field schemas from the registered <see cref="IContentSchemaDefinition"/>
/// services and uses <see cref="IContentSchemaProvider"/> for dynamic enum values.
/// </summary>
public sealed class ContentDefinitionRecipeStep : ContentDefinitionRecipeStepBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionRecipeStep"/> class.
    /// </summary>
    /// <param name="schemaDefinitions">The registered content schema definitions used to compose the step schema.</param>
    /// <param name="contentSchemaProvider">The provider used to resolve dynamic enum values.</param>
    public ContentDefinitionRecipeStep(
        IEnumerable<IContentSchemaDefinition> schemaDefinitions,
        IContentSchemaProvider contentSchemaProvider)
        : base(schemaDefinitions, contentSchemaProvider)
    {
    }

    public override string Name => "ContentDefinition";
}
