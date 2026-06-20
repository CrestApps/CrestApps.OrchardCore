using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Queries" recipe step — defines SQL, Lucene, or other query types.
/// </summary>
public sealed class QueriesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Queries";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Queries").Description("Recipe step discriminator. Must be 'Queries'.")),
                ("Queries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique query name.")),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Query source provider, such as Sql, Lucene, or Elasticsearch.")),
                            ("Schema", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional schema or projection payload consumed by the source provider.")),
                            ("Template", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Raw query text or template executed by the source provider.")),
                            ("ReturnContentItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the query returns full content items instead of primitive results.")))
                        .Required("Name", "Source")
                        .AdditionalProperties(true))
                    .MinItems(1)
                    .Description("Queries to create or update.")))
            .Required("name", "Queries")
            .AdditionalProperties(true)
            .Build();
}
