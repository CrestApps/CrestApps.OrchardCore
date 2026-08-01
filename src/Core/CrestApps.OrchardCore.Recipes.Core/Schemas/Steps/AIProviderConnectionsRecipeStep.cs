using CrestApps.Core.AI;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

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
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier.")),
                ("Source", providerSourceSchema),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique connection name.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Display name.")),
                ("ClientName", clientNameSchema),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common connection endpoint alias for recipe imports.")),
                ("AuthenticationType", azureAuthenticationTypeSchema.Description("Common Azure connection authentication type alias for recipe imports.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common connection API key alias for recipe imports.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Common Azure connection managed identity client ID alias for recipe imports.")),
                ("Properties", propertiesSchema.Description("Provider-specific connection metadata grouped by metadata object name.")))
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
            schema = schema.Enum(values);
        }

        return schema;
    }
}
