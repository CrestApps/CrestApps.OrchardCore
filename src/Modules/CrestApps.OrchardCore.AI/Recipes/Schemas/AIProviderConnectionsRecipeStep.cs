using CrestApps.Core.AI;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Schema for the "AIProviderConnections" recipe step — creates or updates AI provider connections.
/// </summary>
public sealed class AIProviderConnectionsRecipeStep : IRecipeStep
{
    private readonly AIOptions _aiOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionsRecipeStep"/> class.
    /// </summary>
    /// <param name="aiOptions">The AI options.</param>
    public AIProviderConnectionsRecipeStep(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public string Name => "AIProviderConnections";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private JsonSchema CreateSchema()
    {
        const string ApiKeyDescription = "Provider API key in clear text. It is encrypted when stored; a deployment plan exports it blank, so fill it in before importing.";

        static JsonSchemaBuilder AzureAuthenticationType(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Enum("Default", "ManagedIdentity", "ApiKey")
                .Description(description);

        static JsonSchemaBuilder OpenAIMetadata(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API endpoint (an absolute address).")),
                    ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description(ApiKeyDescription)))
                .AdditionalProperties(true)
                .Description(description);

        static JsonSchemaBuilder AzureMetadata(string description)
            => new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Provider API endpoint (an absolute address).")),
                    ("AuthenticationType", AzureAuthenticationType("Azure authentication type. Supported values are Default, ManagedIdentity, or ApiKey.")),
                    ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description(ApiKeyDescription + " Required when AuthenticationType is ApiKey.")),
                    ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional client ID of a user-assigned managed identity. Used when AuthenticationType is ManagedIdentity.")))
                .AdditionalProperties(true)
                .Description(description);

        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("OpenAIConnectionMetadata", OpenAIMetadata("Metadata for OpenAI-compatible connections (Source OpenAI).")),
                ("AzureConnectionMetadata", AzureMetadata("Metadata for Azure OpenAI connections.")),
                ("AzureOpenAIConnectionMetadata", AzureMetadata("Legacy metadata alias for Azure OpenAI connections, read when AzureConnectionMetadata is missing.")),
                ("AzureAIInferenceConnectionMetadata", AzureMetadata("Metadata for Azure AI Inference connections.")))
            .AdditionalProperties(true)
            .Description("Provider-specific connection metadata grouped by metadata object name. Merged into the stored properties.");

        var providerSources = _aiOptions.ConnectionSources.Keys
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var providerSourceSchema = CreateProviderNameSchema(
            providerSources,
            "Connection source/provider identifier for the registered AI provider.");
        var clientNameSchema = CreateProviderNameSchema(
            providerSources,
            "Obsolete alias for Source kept for backward compatibility.");

        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When it matches a stored connection, that connection is updated; otherwise the connection is matched by Name.")),
                ("Source", providerSourceSchema),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique connection name.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name.")),
                ("ClientName", clientNameSchema),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Alias of the provider metadata's Endpoint, read by the OpenAI, Azure OpenAI and Azure AI Inference providers.")),
                ("AuthenticationType", AzureAuthenticationType("Alias of the Azure metadata's AuthenticationType.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Alias of the provider metadata's ApiKey. " + ApiKeyDescription)),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Alias of the Azure metadata's IdentityId.")),
                ("Properties", propertiesSchema))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIProviderConnections").Description("Recipe step discriminator. Must be 'AIProviderConnections'.")),
                ("Connections", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(connectionSchema)
                    .MinItems(1)
                    .Description("The AI provider connections to create or update.")))
            .Required("name", "Connections")
            .AdditionalProperties(true)
            .Build();
    }

    private static JsonSchemaBuilder CreateProviderNameSchema(
        IEnumerable<string> providerSources,
        string description)
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description(description);
        var values = providerSources.ToArray();

        if (values.Length > 0)
        {
            schema = schema.WithSuggestions(values);
        }

        return schema;
    }
}
