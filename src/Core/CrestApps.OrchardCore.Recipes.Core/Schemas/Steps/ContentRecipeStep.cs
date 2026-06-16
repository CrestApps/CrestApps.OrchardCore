using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "content" recipe step — imports content items.
/// </summary>
public sealed class ContentRecipeStep : IRecipeStep
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IContentSchemaDefinition> _schemaDefinitions;

    private JsonSchema _cached;
    public string Name => "content";

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentRecipeStep"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="schemaDefinitions">The content definition schema definitions.</param>
    public ContentRecipeStep(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentSchemaDefinition> schemaDefinitions)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _schemaDefinitions = schemaDefinitions;
    }

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var partSchemas = await BuildPartSchemasAsync(cancellationToken);
        var fieldSchemas = await BuildFieldSchemasAsync(cancellationToken);
        var contentItemSchema = ContentCommonSchemas.CreateContentItemSchema(definitions, partSchemas, fieldSchemas);

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

    private async ValueTask<IReadOnlyDictionary<string, JsonSchemaBuilder>> BuildFieldSchemasAsync(CancellationToken cancellationToken)
    {
        var fieldSchemas = new Dictionary<string, List<JsonSchemaBuilder>>(StringComparer.OrdinalIgnoreCase);

        foreach (var schemaDefinition in _schemaDefinitions.OfType<IContentFieldSchemaDefinition>())
        {
            if (!fieldSchemas.TryGetValue(schemaDefinition.Name, out var fragments))
            {
                fragments = [];
                fieldSchemas[schemaDefinition.Name] = fragments;
            }

            fragments.Add(await schemaDefinition.GetFieldSchemaAsync(cancellationToken));
        }

        return fieldSchemas.ToDictionary(
            pair => pair.Key,
            pair => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(pair.Value.ToArray())
                .AdditionalProperties(true),
            StringComparer.OrdinalIgnoreCase);
    }

    private async ValueTask<IReadOnlyDictionary<string, JsonSchemaBuilder>> BuildPartSchemasAsync(CancellationToken cancellationToken)
    {
        var partSchemas = new Dictionary<string, List<JsonSchemaBuilder>>(StringComparer.OrdinalIgnoreCase);

        foreach (var schemaDefinition in _schemaDefinitions.OfType<IContentPartSchemaDefinition>())
        {
            if (!partSchemas.TryGetValue(schemaDefinition.Name, out var fragments))
            {
                fragments = [];
                partSchemas[schemaDefinition.Name] = fragments;
            }

            fragments.Add(await schemaDefinition.GetPartSchemaAsync(cancellationToken));
        }

        return partSchemas.ToDictionary(
            pair => pair.Key,
            pair => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(pair.Value.ToArray())
                .AdditionalProperties(true),
            StringComparer.OrdinalIgnoreCase);
    }
}
