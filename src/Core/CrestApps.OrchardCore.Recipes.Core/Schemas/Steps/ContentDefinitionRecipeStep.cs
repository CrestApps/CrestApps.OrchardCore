namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContentDefinition" recipe step.
/// Composes part and field schemas from the registered <see cref="IContentSchemaDefinition"/>
/// services and uses <see cref="IContentSchemaProvider"/> for dynamic enum values.
/// </summary>
public sealed class ContentDefinitionRecipeStep(
    IEnumerable<IContentSchemaDefinition> schemaDefinitions,
    IContentSchemaProvider contentSchemaProvider)
: ContentDefinitionRecipeStepBase(schemaDefinitions, contentSchemaProvider)
{
    public override string Name => "ContentDefinition";

    protected override IReadOnlyList<string> RequiredProperties => ["name", "ContentTypes"];
}
