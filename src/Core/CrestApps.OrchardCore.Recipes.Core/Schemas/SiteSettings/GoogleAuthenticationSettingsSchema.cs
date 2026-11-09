using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Google authentication settings.
/// </summary>
public sealed class GoogleAuthenticationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "GoogleAuthenticationSettings";

    /// <summary>
    /// Builds the schema for Google authentication settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Google OAuth authentication.")
            .Properties(
                ("ClientID", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Client ID from the Google Cloud Console.")),
                ("ClientSecret", ProtectedCredential("Google client secret", "ClientSecretSecretName")),
                ("ClientSecretSecretName", SecretName("Google client secret")),
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path where the user-agent will be returned after authentication.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens.")))
            .Required("ClientID")
            .AdditionalProperties(false);
}
