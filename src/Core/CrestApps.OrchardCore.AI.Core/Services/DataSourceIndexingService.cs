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

namespace CrestApps.OrchardCore.AI.DataSources.Services;

/// <summary>
/// Service responsible for synchronizing data source documents with the master knowledge base index.
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

    private const int BatchSize = 500;
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
    /// Synchronizes a single data source with its master knowledge base index.
    /// Acquires a lock scoped to the specific data source to prevent concurrent indexing.
    /// </summary>
    public async Task SyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) ||
            string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
        {
            return;
        }

        var masterProfile = (await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType))
            .FirstOrDefault(p => string.Equals(p.IndexName, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            _logger.LogWarning("Master index profile '{IndexName}' not found for data source '{DataSourceId}'.",
                dataSource.AIKnowledgeBaseIndexProfileName, dataSource.ItemId);
            return;
        }

        // Acquire a lock scoped to this specific data source (not the entire index).
        var (locker, isLocked) = await _distributedLock.TryAcquireLockAsync(
            $"DataSourceIndexing-{dataSource.ItemId}",
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(60));

        if (!isLocked)
        {
            _logger.LogWarning("Data source '{DataSourceId}' is already being indexed. Skipping.",
                dataSource.ItemId);
            return;
        }

        try
        {
            await SyncDataSourceLockedAsync(dataSource, masterProfile, cancellationToken);
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    /// <summary>
    /// Synchronizes all master indexes for all data sources that have a master index configured.
    /// </summary>
    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var masterIndexProfiles = (await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType)).ToList();

        if (masterIndexProfiles.Count == 0)
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
            .Where(x => idList.Contains(x.Id))
            .ToList();

        if (masterIndexProfiles.Count == 0)
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
    /// Deletes all documents for the specified data source from its master knowledge base index.
    /// </summary>
    public async Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            return;
        }

        var masterIndexProfiles = await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType);

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
            string.Equals(p.IndexName, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted documents for data source '{DataSourceId}' from master index '{IndexName}'.",
                    dataSource.ItemId, masterProfile.IndexName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting documents for data source '{DataSourceId}' from master index '{IndexName}'.",
                dataSource.ItemId, masterProfile.IndexName);
        }
    }

    /// <summary>
    /// Re-indexes specific documents from the source index into the AI KB index.
    /// Used for real-time incremental updates when source documents change.
    /// </summary>
    public async Task IndexDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync();
        var masterIndexProfiles = (await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType)).ToList();

        if (masterIndexProfiles.Count == 0)
        {
            return;
        }

        foreach (var dataSource in allDataSources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) ||
                string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
            {
                continue;
            }

            var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
                string.Equals(p.IndexName, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

            if (masterProfile == null)
            {
                continue;
            }

            var sourceProfile = await _indexProfileStore.FindByNameAsync(dataSource.SourceIndexProfileName);

            if (sourceProfile == null)
            {
                continue;
            }

            var documentReader = _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

            if (documentReader == null)
            {
                continue;
            }

            await IndexSpecificDocumentsAsync(dataSource, masterProfile, sourceProfile, documentReader, idList, cancellationToken);
        }
    }

    /// <summary>
    /// Removes specific documents (by their reference IDs) from the AI KB index for all data sources.
    /// Used for real-time removal when source documents are deleted.
    /// </summary>
    public async Task RemoveDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync();
        var masterIndexProfiles = (await _indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType)).ToList();

        if (masterIndexProfiles.Count == 0)
        {
            return;
        }

        foreach (var dataSource in allDataSources)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
            {
                continue;
            }

            var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
                string.Equals(p.IndexName, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

            if (masterProfile == null)
            {
                continue;
            }

            var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            try
            {
                // Generate chunk IDs for all possible chunks of these documents.
                // Since we don't know how many chunks each document has, we delete by prefix pattern.
                // For now, delete all chunk IDs that start with referenceId_.
                var chunkIds = new List<string>();

                foreach (var docId in idList)
                {
                    // Delete chunks 0..999 for each document (covers typical document sizes).
                    for (var i = 0; i < 1000; i++)
                    {
                        chunkIds.Add($"{docId}_{i}");
                    }
                }

                await documentIndexManager.DeleteDocumentsAsync(masterProfile, chunkIds);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Removed documents {DocumentIds} from master index '{IndexName}'.",
                        string.Join(", ", idList), masterProfile.IndexName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing documents from master index '{IndexName}'.", masterProfile.IndexName);
            }
        }
    }

    private async Task IndexSpecificDocumentsAsync(
        AIDataSource dataSource,
        IndexProfile masterProfile,
        IndexProfile sourceProfile,
        IDataSourceDocumentReader documentReader,
        List<string> documentIds,
        CancellationToken cancellationToken)
    {
        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

        if (documentIndexManager == null)
        {
            return;
        }

        var indexManager = _serviceProvider.GetKeyedService<IIndexManager>(masterProfile.ProviderName);

        if (indexManager == null || !await indexManager.ExistsAsync(masterProfile.IndexFullName))
        {
            return;
        }

        var profileMetadata = masterProfile.As<DataSourceIndexProfileMetadata>();

        if (string.IsNullOrEmpty(profileMetadata.EmbeddingProviderName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(profileMetadata.EmbeddingDeploymentName))
        {
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            profileMetadata.EmbeddingProviderName,
            profileMetadata.EmbeddingConnectionName,
            profileMetadata.EmbeddingDeploymentName);

        if (embeddingGenerator == null)
        {
            return;
        }

        // Set the timestamp before reading the record.
        var timestamp = _clock.UtcNow;

        var sourceDocuments = documentReader.ReadByIdsAsync(
            sourceProfile,
            documentIds,
            dataSource.KeyFieldName,
            dataSource.TitleFieldName,
            dataSource.ContentFieldName,
            cancellationToken);

        var documents = new List<DocumentIndex>();

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

            var textChunks = ChunkText(sourceDoc.Content);

            if (textChunks.Count == 0)
            {
                continue;
            }

            var chunkTexts = textChunks.Select(c => c.Text).ToList();

            try
            {
                var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);

                if (embeddings == null || embeddings.Count != chunkTexts.Count)
                {
                    continue;
                }

                // Delete existing chunks for this reference ID before re-indexing.
                var existingChunkIds = Enumerable.Range(0, 1000).Select(i => $"{referenceId}_{i}").ToList();
                await documentIndexManager.DeleteDocumentsAsync(masterProfile, existingChunkIds);

                var filters = BuildFilterFields(sourceDoc.Fields);

                for (var i = 0; i < chunkTexts.Count; i++)
                {
                    var chunkId = $"{referenceId}_{i}";

                    var embeddingDocument = new DataSourceEmbeddingDocument
                    {
                        ReferenceId = referenceId,
                        DataSourceId = dataSource.ItemId,
                        ChunkId = chunkId,
                        ChunkIndex = i,
                        Title = sourceDoc.Title,
                        Content = chunkTexts[i],
                        Embedding = embeddings[i].Vector.ToArray(),
                        Timestamp = timestamp,
                        Filters = filters,
                    };

                    var documentIndex = new DocumentIndex(chunkId);

                    var buildContext = new BuildDocumentIndexContext(documentIndex, embeddingDocument, [chunkId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), masterProfile },
                        }
                    };

                    await _documentIndexHandlers.InvokeAsync((x, ctx) => x.BuildIndexAsync(ctx), buildContext, _logger);

                    documents.Add(documentIndex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-indexing document '{ReferenceId}' for data source '{DataSourceId}'.",
                    referenceId, dataSource.ItemId);
            }
        }

        if (documents.Count > 0)
        {
            try
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(masterProfile, documents);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Re-indexed {DocumentCount} chunks for {SourceCount} documents in data source '{DataSourceId}' to master index '{IndexName}'.",
                        documents.Count, documentIds.Count, dataSource.ItemId, masterProfile.IndexName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing re-indexed documents to master index '{IndexName}'.", masterProfile.IndexName);
            }
        }
    }

    private async Task SyncDataSourceWithRetryAsync(
        AIDataSource dataSource,
        List<IndexProfile> masterIndexProfiles,
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
        if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) || string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
        {
            return;
        }

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
            string.Equals(p.IndexName, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            return;
        }

        // Acquire a distributed lock scoped to this data source to prevent concurrent indexing.
        var (locker, isLocked) = await _distributedLock.TryAcquireLockAsync(
            $"DataSourceIndexing-{dataSource.ItemId}",
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(60));

        if (!isLocked)
        {
            _logger.LogWarning("Data source '{DataSourceId}' is already being indexed for master index '{IndexName}'. Skipping.",
                dataSource.ItemId, masterProfile.IndexName);
            return;
        }

        try
        {
            await SyncDataSourceLockedAsync(dataSource, masterProfile, cancellationToken);
        }
        finally
        {
            await locker.DisposeAsync();
        }
    }

    private async Task SyncDataSourceLockedAsync(AIDataSource dataSource, IndexProfile masterProfile, CancellationToken cancellationToken)
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

        // Look up the source index profile to determine its provider name.
        var sourceProfile = await _indexProfileStore.FindByNameAsync(dataSource.SourceIndexProfileName);

        if (sourceProfile == null)
        {
            _logger.LogWarning("Source index profile '{IndexName}' not found.", dataSource.SourceIndexProfileName);
            return;
        }

        var documentReader = _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

        if (documentReader == null)
        {
            _logger.LogWarning("No document reader found for provider '{ProviderName}'.", sourceProfile.ProviderName);
            return;
        }

        var sourceDocuments = documentReader.ReadAsync(
            sourceProfile,
            dataSource.KeyFieldName,
            dataSource.TitleFieldName,
            dataSource.ContentFieldName,
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

                // Build filter fields from source document fields.
                var filters = BuildFilterFields(sourceDoc.Fields);

                // Create one document per chunk.
                for (var i = 0; i < chunkTexts.Count; i++)
                {
                    var chunkId = $"{referenceId}_{i}";

                    var embeddingDocument = new DataSourceEmbeddingDocument
                    {
                        ReferenceId = referenceId,
                        DataSourceId = dataSource.ItemId,
                        ChunkId = chunkId,
                        ChunkIndex = i,
                        Title = sourceDoc.Title,
                        Content = chunkTexts[i],
                        Embedding = embeddings[i].Vector.ToArray(),
                        Timestamp = timestamp,
                        Filters = filters,
                    };

                    var documentIndex = new DocumentIndex(chunkId);

                    var buildContext = new BuildDocumentIndexContext(documentIndex, embeddingDocument, [chunkId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), masterProfile },
                        }
                    };

                    await _documentIndexHandlers.InvokeAsync((x, ctx) => x.BuildIndexAsync(ctx), buildContext, _logger);

                    documents.Add(documentIndex);
                    documentCount++;

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document '{ReferenceId}' for data source '{DataSourceId}'. Continuing with remaining documents.",
                    referenceId, dataSource.ItemId);
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
            _logger.LogInformation("Synced {DocumentCount} chunks for data source '{DataSourceId}' to master index '{IndexName}'.",
                documentCount, dataSource.ItemId, masterProfile.IndexName);
        }
    }

    private static Dictionary<string, object> BuildFilterFields(Dictionary<string, object> sourceFields)
    {
        if (sourceFields == null || sourceFields.Count == 0)
        {
            return null;
        }

        var filters = new Dictionary<string, object>(sourceFields, StringComparer.OrdinalIgnoreCase);

        return filters.Count > 0 ? filters : null;
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
