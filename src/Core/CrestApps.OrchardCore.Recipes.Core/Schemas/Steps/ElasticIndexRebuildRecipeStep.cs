using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "elastic-index-rebuild" recipe step — rebuilds Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "elastic-index-rebuild";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("elastic-index-rebuild").Description("Recipe step discriminator. Must be 'elastic-index-rebuild'.")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, rebuild all Elasticsearch indexes and ignore the Indices list.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific Elasticsearch index names to rebuild when IncludeAll is false.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
