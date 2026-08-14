using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Azure AD settings.
/// </summary>
public sealed class AzureADSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AzureADSettings";

    /// <summary>
    /// Builds the schema for Azure AD settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Azure Active Directory authentication.")
            .Properties(
                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The display name for the Azure AD login provider.")),
                ("AppId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Application (client) ID from Azure AD app registration.")),
                ("TenantId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Directory (tenant) ID from Azure AD.")),
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path within the application's base path where the user-agent will be returned after sign-in.")),
                ("SaveTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to save the access and refresh tokens in the authentication properties.")))
            .Required("DisplayName", "AppId", "TenantId")
            .AdditionalProperties(false);
}
