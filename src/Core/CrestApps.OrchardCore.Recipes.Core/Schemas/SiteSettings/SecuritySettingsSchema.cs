using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for security settings.
/// </summary>
public sealed class SecuritySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SecuritySettings";

    /// <summary>
    /// Builds the schema for security settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for HTTP security headers.")
            .Properties(
                ("ContentSecurityPolicy", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The Content-Security-Policy header directives.").AdditionalProperties(true)),
                ("ContentTypeOptions", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The X-Content-Type-Options header value.")),
                ("PermissionsPolicy", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The Permissions-Policy header directives.").AdditionalProperties(true)),
                ("ReferrerPolicy", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Referrer-Policy header value.")))
            .AdditionalProperties(false);
}
