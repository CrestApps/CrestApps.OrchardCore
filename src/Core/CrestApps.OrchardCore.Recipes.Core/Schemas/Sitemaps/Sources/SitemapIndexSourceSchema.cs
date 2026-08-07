using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.Sources;

/// <summary>
/// Describes the recipe schema for the <c>SitemapIndexSource</c> sitemap source.
/// </summary>
/// <remarks>
/// A sitemap index references other sitemaps by their identifier. This source is used a single time inside a
/// sitemap index and is not offered as a regular source in the sitemap editor.
/// </remarks>
public sealed class SitemapIndexSourceSchema : SitemapSourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "SitemapIndexSource";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Sitemaps.Models.SitemapIndexSource, OrchardCore.Sitemaps";

    /// <inheritdoc />
    protected override string DisplayText => "Sitemap Index";

    /// <inheritdoc />
    protected override string Description => "References other sitemaps by their identifier to build a sitemap index.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context)
    {
        yield return ("ContainedSitemapIds", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Description("The identifiers of the sitemaps contained in this index. An empty list contains every sitemap."));
    }
}
