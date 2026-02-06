namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Queries" recipe step — defines SQL, Lucene, or other query types.
/// </summary>
public sealed class QueriesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Queries";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Queries")),
                ("Queries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Schema", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Template", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("ReturnContentItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                        .Required("Name", "Source")
                        .AdditionalProperties(true))
                    .MinItems(1)))
            .Required("name", "Queries")
            .AdditionalProperties(true)
            .Build();
}
