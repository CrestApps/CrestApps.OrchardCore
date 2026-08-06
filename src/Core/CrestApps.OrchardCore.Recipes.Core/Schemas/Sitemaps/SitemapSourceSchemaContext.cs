namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

/// <summary>
/// Provides contextual information about a sitemap source while its recipe schema is being built.
/// </summary>
public sealed class SitemapSourceSchemaContext
{
    /// <summary>
    /// Gets the sitemap source name as reported by the schema definition.
    /// </summary>
    public required string SourceName { get; init; }
}
