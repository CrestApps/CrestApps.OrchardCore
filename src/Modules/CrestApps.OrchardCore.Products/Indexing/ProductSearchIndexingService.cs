using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Products.Indexing;

/// <summary>
/// Writes the product search records to every product search index (an Orchard Core index profile of the
/// <see cref="ProductSearchIndexFields.IndexingTaskType"/> type), whichever provider holds it.
/// </summary>
/// <remarks>
/// The records are built once, for the database search, and every index is written from them: a product change
/// writes its record, and a new or reset index is filled from all of them.
/// </remarks>
public sealed class ProductSearchIndexingService
{
    private const int BatchSize = 500;

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly ILogger _logger;

    public ProductSearchIndexingService(
        IIndexProfileStore indexProfileStore,
        ISession session,
        IServiceProvider serviceProvider,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        ILogger<ProductSearchIndexingService> logger)
    {
        _indexProfileStore = indexProfileStore;
        _session = session;
        _serviceProvider = serviceProvider;
        _documentIndexHandlers = documentIndexHandlers;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IndexProfile>> GetIndexProfilesAsync()
        => (await _indexProfileStore.GetByTypeAsync(ProductSearchIndexFields.IndexingTaskType)).ToArray();

    public async Task IndexAsync(IReadOnlyCollection<ProductSearchDocument> documents)
    {
        if (documents is null || documents.Count == 0)
        {
            return;
        }

        foreach (var indexProfile in await GetIndexProfilesAsync())
        {
            var manager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (manager is null)
            {
                continue;
            }

            await manager.AddOrUpdateDocumentsAsync(indexProfile, await BuildAsync(documents, indexProfile, manager));
        }
    }

    public async Task RemoveAsync(IReadOnlyCollection<string> contentItemIds)
    {
        if (contentItemIds is null || contentItemIds.Count == 0)
        {
            return;
        }

        foreach (var indexProfile in await GetIndexProfilesAsync())
        {
            var manager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (manager is not null)
            {
                await manager.DeleteDocumentsAsync(indexProfile, contentItemIds);
            }
        }
    }

    /// <summary>
    /// Empties the index and writes every product search record to it.
    /// </summary>
    public async Task<int> SynchronizeAsync(IndexProfile indexProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        var manager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

        if (manager is null)
        {
            return 0;
        }

        await manager.DeleteAllDocumentsAsync(indexProfile);

        var written = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var documents = (await _session.Query<ProductSearchDocument, ProductSearchIndex>()
                .OrderBy(index => index.Id)
                .Skip(written)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToArray();

            if (documents.Length == 0)
            {
                break;
            }

            await manager.AddOrUpdateDocumentsAsync(indexProfile, await BuildAsync(documents, indexProfile, manager));
            written += documents.Length;

            if (documents.Length < BatchSize)
            {
                break;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Wrote {Count} products to the product search index '{IndexName}'.", written, indexProfile.Name);
        }

        return written;
    }

    /// <summary>
    /// Product types or their attributes changed: each index is saved again, so a provider that declares its
    /// fields up front declares the new ones, then rebuilt and filled again.
    /// </summary>
    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var profileManager = _serviceProvider.GetService<IIndexProfileManager>();

        foreach (var indexProfile in await GetIndexProfilesAsync())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var indexManager = _serviceProvider.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

            if (profileManager is not null)
            {
                await profileManager.UpdateAsync(indexProfile);
            }

            if (indexManager is not null)
            {
                await indexManager.RebuildAsync(indexProfile);
            }

            await SynchronizeAsync(indexProfile, cancellationToken);
        }
    }

    private async Task<List<DocumentIndex>> BuildAsync(IEnumerable<ProductSearchDocument> documents, IndexProfile indexProfile, IDocumentIndexManager manager)
    {
        var settings = manager.GetContentIndexSettings();
        var indexes = new List<DocumentIndex>();

        foreach (var document in documents)
        {
            var documentIndex = new DocumentIndex(document.ContentItemId);
            var context = new BuildDocumentIndexContext(documentIndex, document, [document.ContentItemId], settings)
            {
                AdditionalProperties = new Dictionary<string, object>
                {
                    [nameof(IndexProfile)] = indexProfile,
                },
            };

            await _documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), context, _logger);

            indexes.Add(documentIndex);
        }

        return indexes;
    }
}
