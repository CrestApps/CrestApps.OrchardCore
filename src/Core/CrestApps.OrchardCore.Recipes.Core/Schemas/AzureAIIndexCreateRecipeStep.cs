namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "azureai-index-create" recipe step — creates Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexCreateRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-create";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-create")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true))))
            .Required("name", "Indices")
            .AdditionalProperties(true)
            .Build();
}
