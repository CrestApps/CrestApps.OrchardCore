using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Represents the open id server settings recipe step.
/// </summary>
public sealed class OpenIdServerSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdServerSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
            [
                ("AccessTokenFormat", RecipeStepSchemaBuilders.String().Enum("DataProtection", "JsonWebToken").Description("Format used when issuing access tokens.")),
                ("Authority", RecipeStepSchemaBuilders.String().Description("Public authority or issuer URL exposed by the OpenID server.")),
                ("DisableAccessTokenEncryption", RecipeStepSchemaBuilders.Boolean().Description("Whether encrypted access tokens should be disabled.")),
                ("EncryptionCertificateStoreLocation", RecipeStepSchemaBuilders.String()
                    .Enum("CurrentUser", "LocalMachine")
                    .Description("Windows certificate store location that contains the encryption certificate.")),
                ("EncryptionCertificateStoreName", RecipeStepSchemaBuilders.String()
                    .Enum("AddressBook", "AuthRoot", "CertificateAuthority", "Disallowed", "My", "Root", "TrustedPeople", "TrustedPublisher")
                    .Description("Windows certificate store name that contains the encryption certificate.")),
                ("EncryptionCertificateThumbprint", RecipeStepSchemaBuilders.String().Description("Thumbprint of the certificate used for token encryption.")),
                ("SigningCertificateStoreLocation", RecipeStepSchemaBuilders.String()
                    .Enum("CurrentUser", "LocalMachine")
                    .Description("Windows certificate store location that contains the signing certificate.")),
                ("SigningCertificateStoreName", RecipeStepSchemaBuilders.String()
                    .Enum("AddressBook", "AuthRoot", "CertificateAuthority", "Disallowed", "My", "Root", "TrustedPeople", "TrustedPublisher")
                    .Description("Windows certificate store name that contains the signing certificate.")),
                ("SigningCertificateThumbprint", RecipeStepSchemaBuilders.String().Description("Thumbprint of the certificate used for signing tokens.")),
                ("EnableTokenEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the token endpoint is enabled.")),
                ("EnableAuthorizationEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the authorization endpoint is enabled.")),
                ("EnableLogoutEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the logout endpoint is enabled.")),
                ("EnableUserInfoEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the user info endpoint is enabled.")),
                ("EnableIntrospectionEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the introspection endpoint is enabled.")),
                ("EnablePushedAuthorizationEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the pushed authorization endpoint is enabled.")),
                ("EnableRevocationEndpoint", RecipeStepSchemaBuilders.Boolean().Description("Whether the revocation endpoint is enabled.")),
                ("AllowPasswordFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the resource owner password flow is allowed.")),
                ("AllowClientCredentialsFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the client credentials flow is allowed.")),
                ("AllowAuthorizationCodeFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether the authorization code flow is allowed.")),
                ("AllowRefreshTokenFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether refresh tokens can be exchanged for new access tokens.")),
                ("AllowHybridFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether hybrid response types are allowed.")),
                ("AllowImplicitFlow", RecipeStepSchemaBuilders.Boolean().Description("Whether implicit response types are allowed.")),
                ("DisableRollingRefreshTokens", RecipeStepSchemaBuilders.Boolean().Description("Whether refresh token rolling should be disabled.")),
                ("RequireProofKeyForCodeExchange", RecipeStepSchemaBuilders.Boolean().Description("Whether PKCE is required for authorization code flows.")),
                ("RequirePushedAuthorizationRequests", RecipeStepSchemaBuilders.Boolean().Description("Whether clients must use pushed authorization requests.")),
                ("UseReferenceAccessTokens", RecipeStepSchemaBuilders.Boolean().Description("Whether reference access tokens should be issued instead of self-contained tokens.")),
            ]);
}
