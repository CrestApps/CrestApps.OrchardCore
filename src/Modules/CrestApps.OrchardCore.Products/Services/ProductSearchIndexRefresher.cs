using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Services;

internal sealed class ProductSearchIndexRefresher : IProductSearchIndexRefresher
{
    private readonly IContentManager _contentManager;
    private readonly ProductSearchIndexer _indexer;

    public ProductSearchIndexRefresher(IContentManager contentManager, ProductSearchIndexer indexer)
    {
        _contentManager = contentManager;
        _indexer = indexer;
    }

    public async Task RefreshAsync(IEnumerable<string> contentItemIds, CancellationToken cancellationToken = default)
    {
        foreach (var contentItemId in (contentItemIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var published = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

            if (published is not null && published.Has<ProductPart>())
            {
                await _indexer.IndexAsync(published);
            }
            else
            {
                await _indexer.RemoveAsync(contentItemId);
            }
        }
    }

    public Task RequestRebuildAsync()
        => _indexer.RequestRebuildAsync();
}
