using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for OpenID client settings.
/// </summary>
public sealed class OpenIdClientSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "OpenIdClientSettings";

    /// <summary>
    /// Builds the schema for OpenID client settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for OpenID Connect client authentication.")
            .Properties(
                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The display name for the OpenID Connect login provider.")),
                ("Authority", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI of the OpenID Connect identity provider.")),
                ("ClientId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The client identifier registered with the identity provider.")),
                ("ClientSecret", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The client secret registered with the identity provider.")),
                ("CallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path within the application's base path where the user-agent will be returned.")),
                ("SignedOutRedirectUri", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI to redirect to after signing out.")),
                ("SignedOutCallbackPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The request path for the signed-out callback.")),
                ("Scopes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The scopes to request from the identity provider.")),
                ("ResponseType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The response type expected from the identity provider (e.g., 'code', 'id_token token').")),
                ("ResponseMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("fragment", "query", "form_post").Description("The response mode used when communicating with the identity provider.")),
                ("StoreExternalTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to store external tokens for later retrieval.")),
                ("Parameters", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(
                    new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                        ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The parameter name.")),
                        ("Value", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The parameter value.")))
                    .Required("Name", "Value"))
                    .Description("Additional parameters to send to the identity provider.")))
            .Required("DisplayName", "Authority", "ClientId")
            .AdditionalProperties(false);
}
