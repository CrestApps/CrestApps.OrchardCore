using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public sealed class OpenIdServerSettingsRecipeStep : RecipeStepSchemaBase
{
    public override string Name => "OpenIdServerSettings";

    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(Name,
        [
            ("AccessTokenFormat", RecipeStepSchemaBuilders.String().Enum("DataProtection", "JsonWebToken")),
            ("Authority", RecipeStepSchemaBuilders.String()),
            ("DisableAccessTokenEncryption", RecipeStepSchemaBuilders.Boolean()),
            ("EncryptionCertificateStoreLocation", RecipeStepSchemaBuilders.String()
                .Enum("CurrentUser", "LocalMachine")),
            ("EncryptionCertificateStoreName", RecipeStepSchemaBuilders.String()
                .Enum("AddressBook", "AuthRoot", "CertificateAuthority", "Disallowed", "My", "Root", "TrustedPeople", "TrustedPublisher")),
            ("EncryptionCertificateThumbprint", RecipeStepSchemaBuilders.String()),
            ("SigningCertificateStoreLocation", RecipeStepSchemaBuilders.String()
                .Enum("CurrentUser", "LocalMachine")),
            ("SigningCertificateStoreName", RecipeStepSchemaBuilders.String()
                .Enum("AddressBook", "AuthRoot", "CertificateAuthority", "Disallowed", "My", "Root", "TrustedPeople", "TrustedPublisher")),
            ("SigningCertificateThumbprint", RecipeStepSchemaBuilders.String()),
            ("EnableTokenEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnableAuthorizationEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnableLogoutEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnableUserInfoEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnableIntrospectionEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnablePushedAuthorizationEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("EnableRevocationEndpoint", RecipeStepSchemaBuilders.Boolean()),
            ("AllowPasswordFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowClientCredentialsFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowAuthorizationCodeFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowRefreshTokenFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowHybridFlow", RecipeStepSchemaBuilders.Boolean()),
            ("AllowImplicitFlow", RecipeStepSchemaBuilders.Boolean()),
            ("DisableRollingRefreshTokens", RecipeStepSchemaBuilders.Boolean()),
            ("RequireProofKeyForCodeExchange", RecipeStepSchemaBuilders.Boolean()),
            ("RequirePushedAuthorizationRequests", RecipeStepSchemaBuilders.Boolean()),
            ("UseReferenceAccessTokens", RecipeStepSchemaBuilders.Boolean()),
        ]);
}
