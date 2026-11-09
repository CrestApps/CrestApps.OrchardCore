using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the OpenID Connect server keys stored in secrets.
/// </summary>
public sealed class OpenIdSecretSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "OpenIdSecretSettings";

    /// <summary>
    /// Builds the schema for the OpenID Connect secret settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The secrets that hold the signing and encryption keys of the OpenID Connect server (OrchardCore.OpenId.Secrets feature). A referenced secret takes precedence over the certificates of the OpenIdServerSettings.")
            .Properties(
                ("SigningKeySecretName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Examples("OpenId.SigningCertificate")
                    .Description("The name of the RsaKeySecret or X509Secret that holds the key used to sign tokens.")),
                ("EncryptionKeySecretName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Examples("OpenId.EncryptionCertificate")
                    .Description("The name of the RsaKeySecret or X509Secret that holds the key used to encrypt tokens.")))
            .AdditionalProperties(false);
}
