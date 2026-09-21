using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for anonymous AI chat visitor identity settings.
/// </summary>
public sealed class AIVisitorIdentityOptionsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => nameof(CrestApps.Core.AI.Security.AIVisitorIdentityOptions);

    /// <summary>
    /// Builds the schema for anonymous AI chat visitor identity settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for anonymous AI chat visitor identity and remote-address handling.")
            .Properties(
                ("CookieName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The cookie name used to persist the anonymous visitor identifier.")),
                ("CookieLifetime", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The visitor cookie lifetime as a .NET TimeSpan string.")),
                ("AllowCrossSiteEmbedding", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the visitor cookie is written SameSite=None; Secure so it survives inside a frame on another site. A request that did not arrive over HTTPS keeps the SameSite=Lax cookie either way.")),
                ("UsePartitionedCookie", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the visitor cookie carries the Partitioned attribute while AllowCrossSiteEmbedding is on, which keeps the cookie written in a frame apart from the first-party one of the same name.")),
                ("RemoteAddressMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The remote-address storage mode.")),
                ("RemoteAddressHashSalt", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The application-specific salt used when hashing remote addresses.")))
            .AdditionalProperties(false);
}
