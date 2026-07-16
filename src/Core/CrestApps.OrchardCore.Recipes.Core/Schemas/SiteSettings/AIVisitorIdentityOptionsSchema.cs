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
                ("RemoteAddressMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The remote-address storage mode.")),
                ("RemoteAddressHashSalt", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The application-specific salt used when hashing remote addresses.")))
            .AdditionalProperties(false);
}
