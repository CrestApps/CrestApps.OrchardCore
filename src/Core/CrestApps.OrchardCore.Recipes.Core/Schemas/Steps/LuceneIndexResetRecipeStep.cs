using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "lucene-index-reset" recipe step — resets Lucene indexes.
/// </summary>
public sealed class LuceneIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index-reset";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index-reset").Description("Recipe step discriminator. Must be 'lucene-index-reset'.")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, reset all Lucene indexes and ignore the Indices list.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific Lucene index names to reset when IncludeAll is false.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
