namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "lucene-index-rebuild" recipe step — rebuilds Lucene indexes.
/// </summary>
public sealed class LuceneIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "lucene-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("lucene-index-rebuild")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
