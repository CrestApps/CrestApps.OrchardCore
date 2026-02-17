using Json.Schema;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "content" recipe step — imports content items.
/// </summary>
public sealed class ContentRecipeStep : IRecipeStep
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private JsonSchema _cached;
    public string Name => "content";

    public ContentRecipeStep(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async ValueTask<JsonSchema> GetSchemaAsync()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var contentTypes = definitions.Select(definition => definition.Name).ToArray();

        var contentItemSchema = ContentCommonSchemas.CreateContentItemSchema(contentTypes);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("content")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(contentItemSchema)
                    .MinItems(1)))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
