using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for OpenID server settings.
/// </summary>
public sealed class OpenIdServerSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "OpenIdServerSettings";

    /// <summary>
    /// Builds the schema for OpenID server settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the OpenID Connect server.")
            .Properties(
                ("AccessTokenFormat", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("DataProtection", "JsonWebToken").Description("The format used to create access tokens.")),
                ("Authority", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The base URI of the OpenID Connect server.")),
                ("DisableAccessTokenEncryption", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disable access token encryption.")),
                ("EncryptionCertificateStoreLocation", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("CurrentUser", "LocalMachine").Description("The X.509 certificate store location for the encryption certificate.")),
                ("EncryptionCertificateStoreName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The X.509 certificate store name for the encryption certificate.")),
                ("EncryptionCertificateThumbprint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The thumbprint of the X.509 encryption certificate.")),
                ("SigningCertificateStoreLocation", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("CurrentUser", "LocalMachine").Description("The X.509 certificate store location for the signing certificate.")),
                ("SigningCertificateStoreName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The X.509 certificate store name for the signing certificate.")),
                ("SigningCertificateThumbprint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The thumbprint of the X.509 signing certificate.")),
                ("AuthorizationEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the authorization endpoint.")),
                ("LogoutEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the logout endpoint.")),
                ("TokenEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the token endpoint.")),
                ("UserinfoEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the userinfo endpoint.")),
                ("IntrospectionEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the introspection endpoint.")),
                ("RevocationEndpointPath", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The relative path of the revocation endpoint.")),
                ("AllowPasswordFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow the resource owner password credentials grant.")),
                ("AllowClientCredentialsFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow the client credentials grant.")),
                ("AllowAuthorizationCodeFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow the authorization code grant.")),
                ("AllowRefreshTokenFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow refresh token grants.")),
                ("AllowHybridFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow the hybrid flow.")),
                ("AllowImplicitFlow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to allow the implicit flow.")),
                ("DisableRollingRefreshTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disable rolling refresh tokens.")),
                ("UseReferenceAccessTokens", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use reference access tokens instead of self-contained tokens.")),
                ("RequireProofKeyForCodeExchange", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to require PKCE for authorization code flow.")),
                ("RequirePushedAuthorizationRequests", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to require pushed authorization requests (PAR).")))
            .AdditionalProperties(false);
}
