namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ReplaceContentDefinition" recipe step — replaces content type/part definitions entirely.
/// </summary>
public sealed class ReplaceContentDefinitionRecipeStep(
    IEnumerable<IContentSchemaDefinition> schemaDefinitions,
    IContentSchemaProvider contentSchemaProvider)
: ContentDefinitionRecipeStepBase(schemaDefinitions, contentSchemaProvider)
{
    public override string Name => "ReplaceContentDefinition";
}
