using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the sitemap sources available on the current tenant, used by the
/// <c>Sitemaps</c> recipe step to describe each sitemap and its <c>SitemapSources</c> array.
/// </summary>
public interface ISitemapSchemaService
{
    /// <summary>
    /// Gets a descriptor for every sitemap source contributed through an
    /// <see cref="ISitemapSourceSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<SitemapSourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single entry of a sitemap's <c>SitemapSources</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSourceSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single sitemap object, as used by an entry of the <c>Sitemaps</c>
    /// step's <c>data</c> array.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetSitemapSchemaAsync(CancellationToken cancellationToken = default);
}
