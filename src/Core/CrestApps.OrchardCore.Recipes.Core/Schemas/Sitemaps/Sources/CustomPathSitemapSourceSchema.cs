using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.Sources;

/// <summary>
/// Describes the recipe schema for the <c>CustomPathSitemapSource</c> sitemap source.
/// </summary>
public sealed class CustomPathSitemapSourceSchema : SitemapSourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CustomPathSitemapSource";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Sitemaps.Models.CustomPathSitemapSource, OrchardCore.Sitemaps.Abstractions";

    /// <inheritdoc />
    protected override string DisplayText => "Custom Path";

    /// <inheritdoc />
    protected override string Description => "Adds a single custom URL to the sitemap.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Path"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context)
    {
        yield return ("Path", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The custom URL added to the sitemap."));

        yield return ("LastUpdate", new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description("The last update date reported for the URL, as an ISO 8601 date-time. The system updates this value automatically."));

        yield return ("ChangeFrequency", SitemapSchemaBuilders.ChangeFrequency("The change frequency reported for the URL."));

        yield return ("Priority", SitemapSchemaBuilders.Priority("The priority reported for the URL, from 0 to 10, where 10 maps to the highest sitemap priority of 1.0."));
    }
}
