namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "UrlRewriting" recipe step — creates or updates URL rewrite rules.
/// </summary>
public sealed class UrlRewritingRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "UrlRewriting";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("UrlRewriting")),
                ("Rules", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
