using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "CreateOrUpdateIndexProfile" recipe step — manages index profiles across search providers.
/// </summary>
public sealed class CreateOrUpdateIndexProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "CreateOrUpdateIndexProfile";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("CreateOrUpdateIndexProfile").Description("Recipe step discriminator. Must be 'CreateOrUpdateIndexProfile'.")),
                ("Indexes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier for the index profile.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique index profile name.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Index provider name, such as Lucene, Elasticsearch, or AzureAI.")),
                            ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Index profile type used by the target feature or indexing task.")))
                        .AdditionalProperties(true))
                    .Description("Index profiles to create or update.")))
            .Required("name", "Indexes")
            .AdditionalProperties(true)
            .Build();
}
