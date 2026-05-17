using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for SMTP settings.
/// </summary>
public sealed class SmtpSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SmtpSettings";

    /// <summary>
    /// Builds the schema for SMTP settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the SMTP email provider.")
            .Properties(
                ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the SMTP email provider is enabled.")),
                ("DefaultSender", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The default sender email address.")),
                ("Host", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The SMTP server hostname.")),
                ("Port", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The SMTP server port.").Default(25)),
                ("AutoSelectEncryption", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to automatically select the encryption method.")),
                ("RequireCredentials", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the SMTP server requires authentication.")),
                ("UseDefaultCredentials", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use the default system credentials.")),
                ("EncryptionMethod", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encryption method for the SMTP connection.").Enum("None", "SslTls", "StartTls")),
                ("UserName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The username for SMTP authentication.")),
                ("Password", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The password for SMTP authentication.")),
                ("ProxyHost", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The proxy server hostname.")),
                ("ProxyPort", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The proxy server port.")),
                ("IgnoreInvalidSslCertificate", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to ignore invalid SSL certificates.")),
                ("DeliveryMethod", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The delivery method for sending emails.").Enum("Network", "SpecifiedPickupDirectory")),
                ("PickupDirectoryLocation", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The directory path for storing emails when using the pickup directory delivery method.")))
            .Required("Host")
            .AdditionalProperties(false);
}
