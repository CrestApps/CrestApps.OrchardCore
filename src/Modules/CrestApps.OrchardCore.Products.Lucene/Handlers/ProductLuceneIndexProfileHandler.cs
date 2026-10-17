using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Models;

namespace CrestApps.OrchardCore.Products.Lucene.Handlers;

/// <summary>
/// Names the key field of a Lucene product search index, which Lucene uses to replace and delete a product.
/// </summary>
public sealed class ProductLuceneIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetKeyAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetKeyAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetKeyAsync(context.Model);

    private static Task SetKeyAsync(IndexProfile indexProfile)
    {
        if (!string.Equals(indexProfile.ProviderName, LuceneConstants.ProviderName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(indexProfile.Type, ProductSearchIndexFields.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var metadata = indexProfile.GetOrCreate<LuceneIndexMetadata>();

        metadata.IndexMappings ??= new LuceneIndexMap();
        metadata.IndexMappings.KeyFieldName = ProductSearchIndexFields.ContentItemId;

        indexProfile.Put(metadata);

        return Task.CompletedTask;
    }
}
