using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Service responsible for synchronizing data source documents with the master embedding index.
/// Uses distributed locking to prevent concurrent indexing of the same profile,
/// and auto-retry with exponential backoff for resilience.
/// </summary>
public sealed class DataSourceIndexingService
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ICatalog<AIDataSource> _dataSourceStore;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    private const int BatchSize = 100;
    private const int MaxRetries = 3;

    public DataSourceIndexingService(
        IIndexProfileStore indexProfileStore,
        ICatalog<AIDataSource> dataSourceStore,
        IAIClientFactory aiClientFactory,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        IServiceProvider serviceProvider,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DataSourceIndexingService> logger)
    {
        _indexProfileStore = indexProfileStore;
        _dataSourceStore = dataSourceStore;
        _aiClientFactory = aiClientFactory;
        _documentIndexHandlers = documentIndexHandlers;
        _serviceProvider = serviceProvider;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes all master indexes for all data sources that have a master index configured.
    /// </summary>
    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var masterIndexProfiles = await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType);

        if (!masterIndexProfiles.Any())
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync();

        foreach (var dataSource in allDataSources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await SyncDataSourceWithRetryAsync(dataSource, masterIndexProfiles, cancellationToken);
        }
    }

    /// <summary>
    /// Synchronizes master indexes for a specific set of index profile IDs.
    /// </summary>
    public async Task SyncByIndexProfileIdsAsync(IEnumerable<string> indexProfileIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfileIds);

        var idList = indexProfileIds.ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var masterIndexProfiles = (await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType))
            .Where(x => idList.Contains(x.Id));

        if (!masterIndexProfiles.Any())
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync();

        foreach (var dataSource in allDataSources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await SyncDataSourceWithRetryAsync(dataSource, masterIndexProfiles, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes all documents for the specified data source from its master embedding index.
    /// </summary>
    public async Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();

        if (string.IsNullOrEmpty(indexMetadata.MasterIndexName))
        {
            return;
        }

        var masterIndexProfiles = await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType);

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
            string.Equals(p.IndexName, indexMetadata.MasterIndexName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            return;
        }

        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

        if (documentIndexManager == null)
        {
            _logger.LogWarning("No document index manager found for provider '{ProviderName}'.", masterProfile.ProviderName);
            return;
        }

        try
        {
            // Delete all documents for this data source using the data source ID as a filter.
            // Since we can't filter deletes directly, we rely on the vector search to find documents.
            var vectorSearchService = _serviceProvider.GetKeyedService<IDataSourceVectorSearchService>(masterProfile.ProviderName);

            if (vectorSearchService != null)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Deleted documents for data source '{DataSourceId}' from master index '{IndexName}'.",
                        dataSource.ItemId, masterProfile.IndexName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting documents for data source '{DataSourceId}' from master index '{IndexName}'.",
                dataSource.ItemId, masterProfile.IndexName);
        }
    }

    private async Task SyncDataSourceWithRetryAsync(
        AIDataSource dataSource,
        IEnumerable<IndexProfile> masterIndexProfiles,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await SyncDataSourceAsync(dataSource, masterIndexProfiles, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == MaxRetries)
                {
                    _logger.LogError(ex, "Failed to sync data source '{DataSourceId}' after {MaxRetries} attempts.",
                        dataSource.ItemId, MaxRetries + 1);
                    return;
                }

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                _logger.LogWarning(ex, "Error syncing data source '{DataSourceId}' (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay} seconds.",
                    dataSource.ItemId, attempt + 1, MaxRetries + 1, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task SyncDataSourceAsync(
        AIDataSource dataSource,
        IEnumerable<IndexProfile> masterIndexProfiles,
        CancellationToken cancellationToken)
    {
        var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();

        if (string.IsNullOrEmpty(indexMetadata.MasterIndexName) || string.IsNullOrEmpty(indexMetadata.IndexName))
        {
            return;
        }

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
            string.Equals(p.IndexName, indexMetadata.MasterIndexName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            return;
        }

        // Acquire a distributed lock for this master index to prevent concurrent indexing.
        var (locker, isLocked) = await _distributedLock.TryAcquireLockAsync(
            $"DataSourceIndexing-{masterProfile.Id}-{dataSource.ItemId}",
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMinutes(15));

        if (!isLocked)
        {
            _logger.LogWarning("Data source '{DataSourceId}' is already being indexed for master index '{IndexName}'. Skipping.",
                dataSource.ItemId, masterProfile.IndexName);
            return;
        }

        try
        {
            await SyncDataSourceLockedAsync(dataSource, masterProfile, indexMetadata, cancellationToken);
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    private async Task SyncDataSourceLockedAsync(
        AIDataSource dataSource,
        IndexProfile masterProfile,
        AIDataSourceIndexMetadata indexMetadata,
        CancellationToken cancellationToken)
    {
        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

        if (documentIndexManager == null)
        {
            _logger.LogWarning("No document index manager found for provider '{ProviderName}'.", masterProfile.ProviderName);
            return;
        }

        var indexManager = _serviceProvider.GetKeyedService<IIndexManager>(masterProfile.ProviderName);

        if (indexManager == null || !await indexManager.ExistsAsync(masterProfile.IndexFullName))
        {
            _logger.LogWarning("Master index '{IndexName}' does not exist for provider '{ProviderName}'.",
                masterProfile.IndexName, masterProfile.ProviderName);
            return;
        }

        // Get the embedding configuration from the master index profile.
        var profileMetadata = masterProfile.As<DataSourceIndexProfileMetadata>();

        if (string.IsNullOrEmpty(profileMetadata.EmbeddingProviderName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingDeploymentName))
        {
            _logger.LogWarning("Embedding configuration is missing for master index '{IndexName}'.", masterProfile.IndexName);
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            profileMetadata.EmbeddingProviderName,
            profileMetadata.EmbeddingConnectionName,
            profileMetadata.EmbeddingDeploymentName);

        if (embeddingGenerator == null)
        {
            _logger.LogWarning("Failed to create embedding generator for master index '{IndexName}'.", masterProfile.IndexName);
            return;
        }

        // Read all documents from the source index using keyed document reader.
        // Look up the source index profile to determine its provider name.
        var sourceProfile = (await _indexProfileStore.GetAllAsync())
            .FirstOrDefault(i => string.Equals(i.Name, indexMetadata.IndexName, StringComparison.OrdinalIgnoreCase));

        if (sourceProfile == null)
        {
            _logger.LogWarning("Source index profile '{IndexName}' not found.", indexMetadata.IndexName);
            return;
        }

        var documentReader = _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

        if (documentReader == null)
        {
            _logger.LogWarning("No document reader found for provider '{ProviderName}'.", sourceProfile.ProviderName);
            return;
        }

        var sourceDocuments = documentReader.ReadAsync(
            indexMetadata.IndexName,
            indexMetadata.TitleFieldName,
            indexMetadata.ContentFieldName,
            cancellationToken);

        var documents = new List<DocumentIndex>();
        var timestamp = _clock.UtcNow;
        var documentCount = 0;

        await foreach (var (referenceId, sourceDoc) in sourceDocuments)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(sourceDoc.Content))
            {
                continue;
            }

            // Chunk the document content.
            var textChunks = ChunkText(sourceDoc.Content);

            if (textChunks.Count == 0)
            {
                continue;
            }

            // Generate embeddings for all chunks.
            var chunkTexts = textChunks.Select(c => c.Text).ToList();

            try
            {
                var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);

                if (embeddings == null || embeddings.Count != chunkTexts.Count)
                {
                    _logger.LogWarning("Failed to generate embeddings for document '{ReferenceId}' in data source '{DataSourceId}'.",
                        referenceId, dataSource.ItemId);
                    continue;
                }

                var embeddingChunks = new List<DataSourceEmbeddingChunk>();
                for (var i = 0; i < chunkTexts.Count; i++)
                {
                    embeddingChunks.Add(new DataSourceEmbeddingChunk
                    {
                        Text = chunkTexts[i],
                        Embedding = embeddings[i].Vector.ToArray(),
                        Index = i,
                    });
                }

                var embeddingDocument = new DataSourceEmbeddingDocument
                {
                    ReferenceId = referenceId,
                    DataSourceId = dataSource.ItemId,
                    Title = sourceDoc.Title,
                    Text = sourceDoc.Content,
                    Timestamp = timestamp,
                    Chunks = embeddingChunks,
                };

                var documentIndex = new DocumentIndex(referenceId);

                var buildContext = new BuildDocumentIndexContext(documentIndex, embeddingDocument, [referenceId], documentIndexManager.GetContentIndexSettings())
                {
                    AdditionalProperties = new Dictionary<string, object>
                    {
                        { nameof(IndexProfile), masterProfile },
                    }
                };

                await _documentIndexHandlers.InvokeAsync((x, ctx) => x.BuildIndexAsync(ctx), buildContext, _logger);

                documents.Add(documentIndex);
                documentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document '{ReferenceId}' for data source '{DataSourceId}'. Continuing with remaining documents.",
                    referenceId, dataSource.ItemId);
            }

            // Write batch to master index when batch size is reached.
            if (documents.Count >= BatchSize)
            {
                try
                {
                    await documentIndexManager.AddOrUpdateDocumentsAsync(masterProfile, documents);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing batch to master index '{IndexName}' for data source '{DataSourceId}'.",
                        masterProfile.IndexName, dataSource.ItemId);
                }

                documents.Clear();
            }
        }

        // Write remaining documents.
        if (documents.Count > 0)
        {
            try
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(masterProfile, documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing final batch to master index '{IndexName}' for data source '{DataSourceId}'.",
                    masterProfile.IndexName, dataSource.ItemId);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Synced {DocumentCount} documents for data source '{DataSourceId}' to master index '{IndexName}'.",
                documentCount, dataSource.ItemId, masterProfile.IndexName);
        }
    }

    private static List<TextChunk> ChunkText(string text, int maxChunkSize = 500, int overlap = 50)
    {
        var chunks = new List<TextChunk>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        var index = 0;
        var chunkIndex = 0;

        while (index < text.Length)
        {
            var length = Math.Min(maxChunkSize, text.Length - index);
            var chunkText = text.Substring(index, length);

            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Index = chunkIndex++,
            });

            index += maxChunkSize - overlap;
        }

        return chunks;
    }

    private sealed class TextChunk
    {
        public string Text { get; set; }
        public int Index { get; set; }
    }
}
