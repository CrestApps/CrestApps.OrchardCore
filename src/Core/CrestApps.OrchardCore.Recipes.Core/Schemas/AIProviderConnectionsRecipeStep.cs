using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AIProviderConnections" recipe step — creates or updates AI provider connections.
/// </summary>
public sealed class AIProviderConnectionsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIProviderConnections";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique connection name.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name.")),
                ("ClientName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Client name (e.g., OpenAI, Azure, AzureAIInference, Ollama).")),
                ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Connection properties.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIProviderConnections")),
                ("Connections", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(connectionSchema)
                    .MinItems(1)
                    .Description("The AI provider connections to create or update.")))
            .Required("name", "Connections")
            .AdditionalProperties(true)
            .Build();
    }
}
