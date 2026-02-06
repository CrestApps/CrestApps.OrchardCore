namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "azureai-index-rebuild" recipe step — rebuilds Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexRebuildRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-rebuild";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-rebuild")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
