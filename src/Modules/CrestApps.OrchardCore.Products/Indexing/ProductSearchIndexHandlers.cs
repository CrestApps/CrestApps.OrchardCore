using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Products.Indexing;

/// <summary>
/// Writes each change to the product search records to the product search indexes.
/// </summary>
public sealed class IndexProfileProductSearchIndexHandler : IProductSearchIndexHandler
{
    private readonly ProductSearchIndexingService _indexingService;

    public IndexProfileProductSearchIndexHandler(ProductSearchIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public Task IndexedAsync(IReadOnlyCollection<ProductSearchDocument> documents, CancellationToken cancellationToken = default)
        => _indexingService.IndexAsync(documents);

    public Task RemovedAsync(IReadOnlyCollection<string> contentItemIds, CancellationToken cancellationToken = default)
        => _indexingService.RemoveAsync(contentItemIds);

    public Task RebuiltAsync(CancellationToken cancellationToken = default)
        => _indexingService.RebuildAllAsync(cancellationToken);
}

/// <summary>
/// Fills a product search index when it is created, reset or rebuilt.
/// </summary>
public sealed class ProductSearchIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly ProductSearchIndexingService _indexingService;

    public ProductSearchIndexProfileHandler(ProductSearchIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (ProductSearchIndexFields.IsProductSearchIndex(context.IndexProfile))
        {
            await _indexingService.SynchronizeAsync(context.IndexProfile);
        }
    }
}

/// <summary>
/// Writes a product search record as the fields every provider shares (see <see cref="ProductSearchIndexFields"/>).
/// </summary>
public sealed class ProductSearchDocumentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ProductSearchDocument document ||
            !context.AdditionalProperties.TryGetValue(nameof(IndexProfile), out var profile) ||
            !ProductSearchIndexFields.IsProductSearchIndex(profile as IndexProfile))
        {
            return Task.CompletedTask;
        }

        foreach (var field in ProductSearchIndexFields.GetFields(document))
        {
            // The key is stored, so a provider that returns stored fields only (Lucene) can name the products found.
            var store = field.Name == ProductSearchIndexFields.ContentItemId ? DocumentIndexOptions.Store : DocumentIndexOptions.None;

            foreach (var value in field.Values)
            {
                switch (field.Kind)
                {
                    case ProductIndexFieldKind.Keyword:
                        context.DocumentIndex.Set(field.Name, (string)value, store | DocumentIndexOptions.Keyword);
                        break;

                    case ProductIndexFieldKind.Text:
                        context.DocumentIndex.Set(field.Name, (string)value, store);
                        break;

                    case ProductIndexFieldKind.Number:
                        context.DocumentIndex.Set(field.Name, (decimal?)value, store);
                        break;

                    case ProductIndexFieldKind.Date:
                        var date = (DateTime)value;
                        context.DocumentIndex.Set(field.Name, (DateTimeOffset?)new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)), store);
                        break;
                }
            }
        }

        return Task.CompletedTask;
    }
}
