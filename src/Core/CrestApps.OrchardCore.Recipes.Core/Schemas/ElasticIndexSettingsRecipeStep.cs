using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "ElasticIndexSettings" recipe step — creates or updates Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexSettingsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ElasticIndexSettings";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ElasticIndexSettings")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}
