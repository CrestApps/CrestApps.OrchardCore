using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for HTTPS settings.
/// </summary>
public sealed class HttpsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "HttpsSettings";

    /// <summary>
    /// Builds the schema for HTTPS settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for HTTPS and strict transport security.")
            .Properties(
                ("EnableStrictTransportSecurity", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable HTTP Strict Transport Security (HSTS) headers.")),
                ("RequireHttps", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to require HTTPS for all requests.")),
                ("RequireHttpsPermanent", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to use a permanent (301) redirect for HTTPS redirection.")),
                ("SslPort", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The port number for SSL connections.")))
            .AdditionalProperties(false);
}
