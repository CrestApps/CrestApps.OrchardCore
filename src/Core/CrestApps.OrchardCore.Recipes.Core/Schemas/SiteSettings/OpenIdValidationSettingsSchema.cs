using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for OpenID validation settings.
/// </summary>
public sealed class OpenIdValidationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "OpenIdValidationSettings";

    /// <summary>
    /// Builds the schema for OpenID validation settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for OpenID Connect token validation.")
            .Properties(
                ("Audience", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The audience (resource identifier) that this application accepts tokens for.")),
                ("Authority", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI of the OpenID Connect identity provider used to validate tokens.")),
                ("DisableTokenTypeValidation", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to disable token type validation.")),
                ("Tenant", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The tenant name to use for local token validation.")),
                ("MetadataAddress", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The URI of the OpenID Connect discovery document.")))
            .AdditionalProperties(false);
}
