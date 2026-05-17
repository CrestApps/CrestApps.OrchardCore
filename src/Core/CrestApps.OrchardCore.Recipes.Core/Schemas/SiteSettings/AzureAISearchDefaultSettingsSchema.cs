using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Azure AI Search default settings.
/// </summary>
public sealed class AzureAISearchDefaultSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AzureAISearchDefaultSettings";

    /// <summary>
    /// Builds the schema for Azure AI Search default settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Azure AI Search connection defaults.")
            .Properties(
                ("UseCustomConfiguration", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use custom configuration instead of the default.")),
                ("AuthenticationType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The authentication type for Azure AI Search.").Enum("Default", "ApiKey", "ManagedIdentity")),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Azure AI Search service endpoint URL.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The API key for authenticating with Azure AI Search.")),
                ("IdentityClientId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The client ID of the managed identity to use.")))
            .AdditionalProperties(false);
}
