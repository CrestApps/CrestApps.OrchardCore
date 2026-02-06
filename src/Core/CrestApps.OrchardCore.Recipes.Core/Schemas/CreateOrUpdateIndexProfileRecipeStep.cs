namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "CreateOrUpdateIndexProfile" recipe step — manages index profiles across search providers.
/// </summary>
public sealed class CreateOrUpdateIndexProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "CreateOrUpdateIndexProfile";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("CreateOrUpdateIndexProfile")),
                ("Indexes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .Required("Name", "ProviderName")
                        .AdditionalProperties(true))))
            .Required("name", "Indexes")
            .AdditionalProperties(true)
            .Build();
}
