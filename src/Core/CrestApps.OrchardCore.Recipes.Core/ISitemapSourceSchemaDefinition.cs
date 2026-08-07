using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single sitemap source inside the
/// <c>SitemapSources</c> array of the <c>Sitemaps</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom sitemap source and wants the generated recipe
/// schema to describe the source's members. Registering the implementation as
/// <see cref="ISitemapSourceSchemaDefinition"/> is enough for the <c>Sitemaps</c> recipe step to pick it up.
/// Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.SitemapSourceSchemaDefinitionBase"/>, which
/// handles the standard source envelope.
/// </remarks>
public interface ISitemapSourceSchemaDefinition
{
    /// <summary>
    /// Gets the sitemap source name that this definition describes, for example
    /// <c>ContentTypesSitemapSource</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the polymorphic type discriminator serialized as the <c>$type</c> member, for example
    /// <c>OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions</c>.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Builds the schema and metadata describing the sitemap source.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<SitemapSourceSchema> GetSourceSchemaAsync(SitemapSourceSchemaContext context, CancellationToken cancellationToken = default);
}
