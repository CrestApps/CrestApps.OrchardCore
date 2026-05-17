using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for GitHub authentication settings.
/// </summary>
public sealed class GitHubAuthenticationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "GitHubAuthenticationSettings";

    /// <summary>
    /// Builds the schema for GitHub authentication settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for GitHub OAuth authentication.")
            .Properties(
                ("ClientID", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Client ID from the GitHub OAuth application.")),
                ("ClientSecret", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Client Secret from the GitHub OAuth application.")),
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path within the application's base path where the user-agent will be returned after sign-in from GitHub.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens in the authentication properties.")))
            .Required("ClientID", "ClientSecret")
            .AdditionalProperties(false);
}
