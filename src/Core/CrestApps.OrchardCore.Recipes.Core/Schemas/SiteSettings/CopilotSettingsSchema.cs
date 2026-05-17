using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Copilot settings.
/// </summary>
public sealed class CopilotSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "CopilotSettings";

    /// <summary>
    /// Builds the schema for Copilot settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the GitHub Copilot-style chat experience.")
            .Properties(
                ("AuthenticationType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("NotConfigured", "GitHubOAuth", "ApiKey").Description("The authentication method used by the Copilot integration.")),
                ("ClientId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The OAuth Client ID for GitHub OAuth authentication.")),
                ("ProtectedClientSecret", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encrypted OAuth Client Secret.")),
                ("Scopes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The OAuth scopes requested during authentication.")),
                ("ProviderType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The AI provider type (e.g., OpenAI, Azure).")),
                ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The base URL for the AI provider API.")),
                ("ProtectedApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encrypted API key for the AI provider.")),
                ("WireApi", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The wire API protocol to use for communication.")),
                ("DefaultModel", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The default AI model to use for Copilot interactions.")),
                ("AzureApiVersion", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Azure OpenAI API version when using Azure as the provider.")))
            .AdditionalProperties(false);
}
