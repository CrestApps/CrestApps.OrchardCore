namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Sitemaps" recipe step — creates or updates sitemaps.
/// </summary>
public sealed class SitemapsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Sitemaps";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Sitemaps")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
    }
}
