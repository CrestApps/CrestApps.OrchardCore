namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "ReplaceContentDefinition" recipe step — replaces content type/part definitions entirely.
/// </summary>
public sealed class ReplaceContentDefinitionRecipeStep(
    IEnumerable<IContentDefinitionSchemaDefinition> schemaDefinitions,
    IContentSchemaProvider contentSchemaProvider)
    : ContentDefinitionRecipeStepBase(schemaDefinitions, contentSchemaProvider)
{
    public override string Name => "ReplaceContentDefinition";
}
