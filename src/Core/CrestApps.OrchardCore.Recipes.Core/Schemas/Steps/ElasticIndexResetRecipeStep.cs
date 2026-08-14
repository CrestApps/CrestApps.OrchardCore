using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "elastic-index-reset" recipe step — resets Elasticsearch indexes.
/// </summary>
public sealed class ElasticIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "elastic-index-reset";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("elastic-index-reset").Description("Recipe step discriminator. Must be 'elastic-index-reset'.")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, reset all Elasticsearch indexes and ignore the Indices list.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific Elasticsearch index names to reset when IncludeAll is false.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
