using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AIDeployment" recipe step — creates or updates AI model deployments.
/// </summary>
public sealed class AIDeploymentRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIDeployment";

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

        var containedConnectionPropertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint. Used by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key. Required when AuthenticationType is ApiKey.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional client ID of a user-assigned managed identity. Used when AuthenticationType is ManagedIdentity.")))
            .AdditionalProperties(true)
            .Description("Provider-specific deployment properties. AzureSpeech deployments use Endpoint, AuthenticationType, ApiKey, and optional IdentityId.");

        var deploymentSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment name as specified by the vendor.")),
                ("ModelName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional vendor model name. Defaults to Name when omitted.")),
                ("ClientName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Client name (e.g., OpenAI, Azure, AzureAIInference, Ollama, AzureSpeech).")),
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Connection name used to configure the provider.")),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema.Description("Contained-connection authentication type alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection managed identity client ID alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("Properties", containedConnectionPropertiesSchema),
                ("Type", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The deployment type, or a comma-separated flag value such as 'Chat, Utility'. Defaults to Chat when not specified."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(
                        new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "Embedding", "Image", "SpeechToText", "TextToSpeech")).MinItems(1).UniqueItems(true).Description("The deployment types."))))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIDeployment")),
                ("Deployments", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(deploymentSchema)
                    .MinItems(1)
                    .Description("The AI deployments to create or update.")))
            .Required("name", "Deployments")
            .AdditionalProperties(true)
            .Build();
    }
}
