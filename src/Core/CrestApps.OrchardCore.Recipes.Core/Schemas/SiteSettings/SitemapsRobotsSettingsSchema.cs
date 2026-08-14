using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for sitemaps robots settings.
/// </summary>
public sealed class SitemapsRobotsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SitemapsRobotsSettings";

    /// <summary>
    /// Builds the schema for sitemaps robots settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for including sitemaps in the robots.txt file.")
            .Properties(
                ("IncludeSitemaps", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to include sitemap URLs in the robots.txt file.").Default(true)))
            .AdditionalProperties(false);
}
