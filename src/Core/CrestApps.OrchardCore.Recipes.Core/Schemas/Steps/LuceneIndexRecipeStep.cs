using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "lucene-index" recipe step — creates or updates Lucene search indexes.
/// </summary>
public sealed class LuceneIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index").Description("Recipe step discriminator. Must be 'lucene-index'.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))
                    .Description("Lucene index definitions to create or update. Each object is forwarded to the Lucene index handler.")))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}
