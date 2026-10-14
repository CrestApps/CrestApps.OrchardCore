using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Handlers;

/// <summary>
/// Refreshes a product's search record when it is published, unpublished, deleted or imported, so a listing shows
/// what is live without waiting for a rebuild. Publishing a taxonomy refreshes the products filed under it, so a
/// renamed term shows its new name and slug.
/// </summary>
/// <remarks>
/// The handler only notes what changed. The records are refreshed once the current scope has completed, in a
/// scope of their own: reading the index from inside a content handler flushes the session in the middle of the
/// caller's work, which breaks a recipe or deployment import half way through its batch. A separate scope also
/// keeps a failed refresh from rolling back the content change itself, and it means the handler never takes the
/// indexer as a dependency, which would be circular through <see cref="IContentManager"/>.
/// </remarks>
public sealed class ProductSearchContentHandler : ContentHandlerBase
{
    private HashSet<string> _products;
    private HashSet<string> _taxonomies;

    public override Task PublishedAsync(PublishContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task ImportedAsync(ImportContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context)
        => TrackAsync(context.ContentItem);

    private Task TrackAsync(ContentItem contentItem)
    {
        if (string.IsNullOrEmpty(contentItem.ContentItemId))
        {
            return Task.CompletedTask;
        }

        if (contentItem.Has<ProductPart>())
        {
            EnsureScheduled();
            _products.Add(contentItem.ContentItemId);
        }
        else if (((JsonObject)contentItem.Content)["TaxonomyPart"] is JsonObject)
        {
            EnsureScheduled();
            _taxonomies.Add(contentItem.ContentItemId);
        }

        return Task.CompletedTask;
    }

    private void EnsureScheduled()
    {
        if (_products is not null)
        {
            return;
        }

        var products = _products = new HashSet<string>(StringComparer.Ordinal);
        var taxonomies = _taxonomies = new HashSet<string>(StringComparer.Ordinal);

        ShellScope.AddDeferredTask(scope => RefreshAsync(scope.ServiceProvider, products, taxonomies));
    }

    private static async Task RefreshAsync(IServiceProvider services, HashSet<string> contentItemIds, HashSet<string> taxonomyIds)
    {
        var contentManager = services.GetRequiredService<IContentManager>();
        var indexer = services.GetRequiredService<ProductSearchIndexer>();

        if (taxonomyIds.Count > 0)
        {
            foreach (var id in await GetProductsFiledUnderAsync(services, taxonomyIds))
            {
                contentItemIds.Add(id);
            }
        }

        foreach (var contentItemId in contentItemIds)
        {
            var published = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

            if (published is not null && published.Has<ProductPart>())
            {
                await indexer.IndexAsync(published);
            }
            else
            {
                await indexer.RemoveAsync(contentItemId);
            }
        }
    }

    /// <summary>
    /// The products with a value from one of the taxonomies, found through the attributes that pick from them.
    /// </summary>
    private static async Task<IEnumerable<string>> GetProductsFiledUnderAsync(IServiceProvider services, HashSet<string> taxonomyIds)
    {
        var catalog = services.GetRequiredService<ProductAttributeCatalog>();
        var keys = (await catalog.GetAttributesAsync([]))
            .Where(attribute => attribute.TaxonomyContentItemId is not null && taxonomyIds.Contains(attribute.TaxonomyContentItemId))
            .Select(attribute => attribute.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (keys.Length == 0)
        {
            return [];
        }

        var session = services.GetRequiredService<ISession>();
        var rows = await session.QueryIndex<ProductAttributeIndex>(index => index.AttributeKey.IsIn(keys)).ListAsync();

        return rows.Select(row => row.ContentItemId).Distinct(StringComparer.Ordinal);
    }
}
