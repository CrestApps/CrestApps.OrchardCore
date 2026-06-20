using CrestApps.Core.AI;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AIDeployment" recipe step — creates or updates AI model deployments.
/// </summary>
public sealed class AIDeploymentRecipeStep : IRecipeStep
{
    private readonly AIOptions _aiOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentRecipeStep"/> class.
    /// </summary>
    /// <param name="aiOptions">The AI options.</param>
    public AIDeploymentRecipeStep(IOptions<AIOptions> aiOptions)
    {
        _aiOptions = aiOptions.Value;
    }

    public string Name => "AIDeployment";

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

        var deploymentPurposeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Chat", "Utility", "Embedding", "Image", "SpeechToText", "TextToSpeech", "Vision")
            .Description("Deployment purpose identifier.");

        var containedConnectionPropertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint. Used by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key. Required when AuthenticationType is ApiKey.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional client ID of a user-assigned managed identity. Used when AuthenticationType is ManagedIdentity.")))
            .AdditionalProperties(true)
            .Description("Provider-specific deployment properties. AzureSpeech deployments use Endpoint, AuthenticationType, ApiKey, and optional IdentityId.");

        var clientNameSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("Client name for the registered AI provider.");
        var clientNames = _aiOptions.CompletionClients.Values
            .Select(static entry => entry.ClientName)
            .Concat(_aiOptions.Deployments.Keys)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clientNames.Length > 0)
        {
            clientNameSchema = clientNameSchema.Enum(clientNames);
        }

        var deploymentSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment name as specified by the vendor.")),
                ("ModelName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional vendor model name. Defaults to Name when omitted.")),
                ("ClientName", clientNameSchema),
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Connection name used to configure the provider.")),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema.Description("Contained-connection authentication type alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection managed identity client ID alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("Properties", containedConnectionPropertiesSchema.Description("Contained provider connection properties stored directly on the deployment.")),
                ("Purpose", new JsonSchemaBuilder().AnyOf(
                    deploymentPurposeSchema.Description("The deployment purpose. Defaults to Chat when not specified."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(
                        deploymentPurposeSchema).MinItems(1).UniqueItems(true).Description("The deployment purposes."))))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIDeployment").Description("Recipe step discriminator. Must be 'AIDeployment'.")),
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
