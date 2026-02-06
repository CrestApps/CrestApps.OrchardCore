namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "elastic-index-rebuild" recipe step — rebuilds Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "elastic-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("elastic-index-rebuild")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}
