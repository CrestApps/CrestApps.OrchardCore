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
        var azureAuthenticationTypeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Default", "ManagedIdentity", "ApiKey")
            .Description("Azure authentication type. Supported values are Default, ManagedIdentity, or ApiKey.");

        var openAIMetadataSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API endpoint.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API key.")))
            .AdditionalProperties(true);

        var azureMetadataSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API endpoint.")),
                ("AuthenticationType", azureAuthenticationTypeSchema),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API key. Required when AuthenticationType is ApiKey.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional client ID of a user-assigned managed identity. Used when AuthenticationType is ManagedIdentity.")))
            .AdditionalProperties(true);

        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("OpenAIConnectionMetadata", openAIMetadataSchema.Description("Metadata for OpenAI-compatible connections.")),
                ("AzureConnectionMetadata", azureMetadataSchema.Description("Metadata for Azure OpenAI connections.")),
                ("AzureOpenAIConnectionMetadata", azureMetadataSchema.Description("Legacy metadata alias for Azure OpenAI connections.")),
                ("AzureAIInferenceConnectionMetadata", azureMetadataSchema.Description("Metadata for Azure AI Inference connections.")))
            .AdditionalProperties(true)
            .Description("Provider-specific connection metadata. Recipe exports keep provider settings under these metadata objects.");

        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Connection source/provider identifier (for example OpenAI, Azure, or AzureAIInference).")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique connection name.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name.")),
                ("ClientName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Obsolete alias for Source kept for backward compatibility.")),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common connection endpoint alias for recipe imports.")),
                ("AuthenticationType", azureAuthenticationTypeSchema.Description("Common Azure connection authentication type alias for recipe imports.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common connection API key alias for recipe imports.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common Azure connection managed identity client ID alias for recipe imports.")),
                ("Properties", propertiesSchema))
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
