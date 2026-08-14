using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "McpConnection" recipe step — creates or updates MCP client connections.
/// </summary>
public sealed class McpConnectionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "McpConnection";

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
        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Transport-specific MCP client metadata grouped by metadata object name. Known built-in metadata objects are SseMcpConnectionMetadata and StdioMcpConnectionMetadata, and extra objects are allowed for future transport extensions.")
            .Properties(
                ("SseMcpConnectionMetadata", KnownRecipeMetadataSchemaBuilders.BuildSseMcpConnectionMetadataSchema()),
                ("StdioMcpConnectionMetadata", KnownRecipeMetadataSchemaBuilders.BuildStdioMcpConnectionMetadataSchema()))
            .AdditionalProperties(true);

        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing MCP connection is updated.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Human-readable name shown for the MCP connection in the admin UI.")),
                ("Source", RecipeStepSchemaBuilders.String().Description("MCP transport type. Known built-in values are 'sse' for remote HTTP/SSE servers and 'stdio' for local process-based servers.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the connection.")),
                ("Properties", propertiesSchema))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "McpConnection",
            [("Connections", RecipeStepSchemaBuilders.Array(connectionSchema, 1).Description("The MCP connections to create or update."))],
            ["Connections"]);
    }
}
