namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ReplaceContentDefinition" recipe step — replaces content type/part definitions entirely.
/// </summary>
public sealed class ReplaceContentDefinitionRecipeStep : ContentDefinitionRecipeStepBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceContentDefinitionRecipeStep"/> class.
    /// </summary>
    /// <param name="schemaDefinitions">The registered content schema definitions used to compose the step schema.</param>
    /// <param name="contentSchemaProvider">The provider used to resolve dynamic enum values.</param>
    public ReplaceContentDefinitionRecipeStep(
        IEnumerable<IContentSchemaDefinition> schemaDefinitions,
        IContentSchemaProvider contentSchemaProvider)
        : base(schemaDefinitions, contentSchemaProvider)
    {
    }

    public override string Name => "ReplaceContentDefinition";
}
