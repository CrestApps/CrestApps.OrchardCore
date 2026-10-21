using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Models;

namespace CrestApps.OrchardCore.Products.Lucene.Handlers;

/// <summary>
/// Names the key field of a Lucene product search index, which Lucene uses to replace and delete a product.
/// </summary>
public sealed class ProductLuceneIndexProfileHandler : ProductSearchIndexProfileHandlerBase
{
    public ProductLuceneIndexProfileHandler()
        : base(LuceneConstants.ProviderName)
    {
    }

    /// <inheritdoc/>
    protected override Task ConfigureAsync(IndexProfile indexProfile)
    {
        var metadata = indexProfile.GetOrCreate<LuceneIndexMetadata>();

        metadata.IndexMappings ??= new LuceneIndexMap();
        metadata.IndexMappings.KeyFieldName = ProductSearchIndexFields.ContentItemId;

        indexProfile.Put(metadata);

        return Task.CompletedTask;
    }
}
