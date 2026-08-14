using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "azureai-index-create" recipe step — creates Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexCreateRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-create";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-create").Description("Recipe step discriminator. Must be 'azureai-index-create'.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))
                    .Description("Azure AI Search index definitions to create. Each object is forwarded to the Azure AI index recipe handler.")))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}
