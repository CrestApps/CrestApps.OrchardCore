using System.Diagnostics;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Service responsible for synchronizing data source documents with the master knowledge base index.
/// Uses distributed locking to prevent concurrent indexing of the same profile,
/// and auto-retry with exponential backoff for resilience.
/// </summary>
public sealed class DataSourceIndexingService
{
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAITextNormalizer _textNormalizer;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedLock _distributedLock;

    private readonly IClock _clock;
    private readonly ILogger _logger;

    private const int BatchSize = 500;
    private const int MaxRetries = 3;

    // The lock.area of the data source indexing lock, and the area of its retries, on the platform instruments.
    private const string TelemetryArea = "ai.datasource_indexing";

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceIndexingService"/> class.
    /// </summary>
    /// <param name="indexProfileManager">The index profile manager.</param>
    /// <param name="dataSourceStore">The store for AI data source definitions.</param>
    /// <param name="deploymentManager">The deployment manager for resolving embedding deployments.</param>
    /// <param name="aiClientFactory">The factory for creating AI clients.</param>
    /// <param name="textNormalizer">The text normalizer for cleaning document content.</param>
    /// <param name="documentIndexHandlers">The handlers for building document index entries.</param>
    /// <param name="serviceProvider">The service provider for resolving keyed services.</param>
    /// <param name="distributedLock">The distributed lock for preventing concurrent indexing.</param>
    /// <param name="clock">The clock for UTC timestamps.</param>
    /// <param name="logger">The logger instance.</param>
    public DataSourceIndexingService(
        IIndexProfileManager indexProfileManager,
        IAIDataSourceStore dataSourceStore,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IAITextNormalizer textNormalizer,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        IServiceProvider serviceProvider,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DataSourceIndexingService> logger)
    {
        _indexProfileManager = indexProfileManager;
        _dataSourceStore = dataSourceStore;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _textNormalizer = textNormalizer;
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
            !HasSourceConfiguration(dataSource))
        {
            return;
        }

        var masterProfile = await _indexProfileManager.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

        if (masterProfile == null)
        {
            _logger.LogWarning("Master index profile '{IndexName}' not found for data source '{DataSourceId}'.",
            dataSource.AIKnowledgeBaseIndexProfileName, dataSource.ItemId);

            return;
        }

        if (masterProfile.Type != DataSourceConstants.IndexingTaskType)
        {
            _logger.LogWarning("Master index profile '{IndexName}' has invalid index type for data source '{DataSourceId}'.",
            dataSource.AIKnowledgeBaseIndexProfileName, dataSource.ItemId);

            return;
        }

        // Acquire a lock scoped to this specific data source (not the entire index).
        var (locker, isLocked) = await _distributedLock.TryAcquireMeasuredLockAsync(
            $"DataSourceIndexing-{dataSource.ItemId}",
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(60),
            TelemetryArea);

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
        var masterIndexProfiles = await GetMasterIndexProfilesAsync();

        if (masterIndexProfiles.Count == 0)
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync(cancellationToken);

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

        var masterIndexProfiles = (await GetMasterIndexProfilesAsync())
            .Where(x => idList.Contains(x.Id))
            .ToList();

        if (masterIndexProfiles.Count == 0)
        {
            return;
        }

        var allDataSources = await _dataSourceStore.GetAllAsync(cancellationToken);

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

        var masterIndexProfiles = await GetMasterIndexProfilesAsync();

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
        string.Equals(p.Name, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            return;
        }

        var contentManager = _serviceProvider.GetKeyedService<IDataSourceContentManager>(masterProfile.ProviderName);

        if (contentManager == null)
        {
            _logger.LogWarning("No vector search service found for provider '{ProviderName}'. Unable to delete documents for data source '{DataSourceId}'.",
            masterProfile.ProviderName, dataSource.ItemId);

            return;
        }

        try
        {
            var deleted = await contentManager.DeleteByDataSourceIdAsync(masterProfile.ToIndexProfileInfo(), dataSource.ItemId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted {DeletedCount} document chunks for data source '{DataSourceId}' from master index '{IndexName}'.",
                deleted, dataSource.ItemId, masterProfile.IndexName);
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
        await IndexDocumentsAsync(null, documentIds, cancellationToken);
    }

    /// <summary>
    /// Re-indexes specific documents from a matching source index into mapped AI KB indexes.
    /// Used for real-time incremental updates when source documents change.
    /// </summary>
    public async Task IndexDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var allDataSources = await GetMatchingDataSourcesAsync(sourceIndexProfileName, incrementalOnly: true);
        var masterIndexProfiles = await GetMasterIndexProfilesAsync();

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
                !HasSourceConfiguration(dataSource))
            {
                continue;
            }

            var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
                string.Equals(p.Name, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

            if (masterProfile == null)
            {
                continue;
            }

            var sourceHandler = ResolveSourceHandler(dataSource);

            if (sourceHandler == null)
            {
                continue;
            }

            await IndexSpecificDocumentsAsync(dataSource, masterProfile, sourceHandler, idList, cancellationToken);
        }
    }

    /// <summary>
    /// Re-indexes specific documents for a single AI data source mapping.
    /// </summary>
    /// <param name="dataSourceId">The AI data source identifier.</param>
    /// <param name="documentIds">The source document identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SyncDataSourceDocumentsAsync(
        string dataSourceId,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceId, cancellationToken);

        if (dataSource == null ||
            string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) ||
            !HasSourceConfiguration(dataSource))
        {
            return;
        }

        var masterProfile = await ResolveMasterProfileAsync(dataSource.AIKnowledgeBaseIndexProfileName, cancellationToken);

        if (masterProfile == null)
        {
            return;
        }

        var sourceHandler = ResolveSourceHandler(dataSource);

        if (sourceHandler == null)
        {
            return;
        }

        await IndexSpecificDocumentsAsync(dataSource, masterProfile, sourceHandler, idList, cancellationToken);
    }

    /// <summary>
    /// Removes specific documents (by their reference IDs) from the AI KB index for all data sources.
    /// Used for real-time removal when source documents are deleted.
    /// </summary>
    public async Task RemoveDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        await RemoveDocumentsAsync(null, documentIds, cancellationToken);
    }

    /// <summary>
    /// Removes specific documents from AI KB indexes mapped to the matching source profile.
    /// Used for real-time removal when source documents are deleted.
    /// </summary>
    public async Task RemoveDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var allDataSources = await GetMatchingDataSourcesAsync(sourceIndexProfileName, incrementalOnly: true);
        var masterIndexProfiles = await GetMasterIndexProfilesAsync();

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
            string.Equals(p.Name, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

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

                // Counted by source document; the chunk ids above are guesses, not chunks that existed.
                AIModuleTelemetry.RecordDocuments(idList.Count, masterProfile.ProviderName, AIModuleTelemetry.ModeIncremental, AIModuleTelemetry.ActionRemoved);

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

    /// <summary>
    /// Removes specific source documents for a single AI data source mapping.
    /// </summary>
    /// <param name="dataSourceId">The AI data source identifier.</param>
    /// <param name="documentIds">The source document identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RemoveDataSourceDocumentsAsync(
        string dataSourceId,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);
        ArgumentNullException.ThrowIfNull(documentIds);

        var idList = documentIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (idList.Count == 0)
        {
            return;
        }

        var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceId, cancellationToken);

        if (dataSource == null || string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            return;
        }

        var masterProfile = await ResolveMasterProfileAsync(dataSource.AIKnowledgeBaseIndexProfileName, cancellationToken);

        if (masterProfile == null)
        {
            return;
        }

        var documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

        if (documentIndexManager == null)
        {
            return;
        }

        try
        {
            var chunkIds = new List<string>();

            foreach (var docId in idList)
            {
                for (var i = 0; i < 1000; i++)
                {
                    chunkIds.Add($"{docId}_{i}");
                }
            }

            await documentIndexManager.DeleteDocumentsAsync(masterProfile, chunkIds);

            AIModuleTelemetry.RecordDocuments(idList.Count, masterProfile.ProviderName, AIModuleTelemetry.ModeIncremental, AIModuleTelemetry.ActionRemoved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing documents from master index '{IndexName}' for data source '{DataSourceId}'.", masterProfile.IndexName, dataSourceId);
        }
    }

    private async Task IndexSpecificDocumentsAsync(
        AIDataSource dataSource,
        IndexProfile masterProfile,
        IAIDataSourceSourceHandler sourceHandler,
        List<string> documentIds,
        CancellationToken cancellationToken)
    {
        var progress = new SyncProgress();
        using var telemetry = StartSyncTelemetry(dataSource, masterProfile, AIModuleTelemetry.ModeIncremental);

        try
        {
            await IndexSpecificDocumentsCoreAsync(dataSource, masterProfile, sourceHandler, documentIds, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            RecordSyncedDocuments(masterProfile, AIModuleTelemetry.ModeIncremental, progress);

            throw;
        }

        CompleteSyncTelemetry(telemetry, masterProfile, AIModuleTelemetry.ModeIncremental, progress, cancellationToken);
    }

    private async Task IndexSpecificDocumentsCoreAsync(
        AIDataSource dataSource,
        IndexProfile masterProfile,
        IAIDataSourceSourceHandler sourceHandler,
        List<string> documentIds,
        SyncProgress progress,
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

        var deployment = await ResolveEmbeddingDeploymentAsync(masterProfile, cancellationToken);

        if (deployment == null)
        {
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(purpose: AIUsagePurposes.Indexing));

        if (embeddingGenerator == null)
        {
            return;
        }

        progress.Started = true;

        // Set the timestamp before reading the record.
        var timestamp = _clock.UtcNow;

        var sourceDocuments = sourceHandler.ReadByIdsAsync(dataSource, documentIds, cancellationToken);
        var referenceType = await sourceHandler.GetReferenceTypeAsync(dataSource, cancellationToken);

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

            // Normalize content and title, then chunk using token-aware splitter.

            var normalizedTitle = _textNormalizer.NormalizeTitle(sourceDoc.Title);

            sourceDoc.Title = normalizedTitle;

            var chunkTexts = await _textNormalizer.NormalizeAndChunkAsync(sourceDoc.Content, cancellationToken);

            if (chunkTexts.Count == 0)
            {
                continue;
            }

            // Prepend the title to the first chunk so it is embedded and becomes searchable.
            if (!string.IsNullOrWhiteSpace(sourceDoc.Title))
            {
                chunkTexts[0] = normalizedTitle + "\n" + chunkTexts[0];
            }

            try
            {
                var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);

                if (embeddings == null || embeddings.Count != chunkTexts.Count)
                {
                    progress.Failed++;

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
                        ReferenceType = referenceType,
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
                    progress.Chunks++;
                }

                progress.Indexed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error re-indexing document '{ReferenceId}' for data source '{DataSourceId}'.",
                referenceId, dataSource.ItemId);

                progress.Failed++;
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

                progress.WriteFailed = true;
            }
        }
    }

    private async Task<IReadOnlyCollection<AIDataSource>> GetMatchingDataSourcesAsync(
        string sourceIndexProfileName,
        bool incrementalOnly = false)
    {
        var dataSources = (await _dataSourceStore.GetAllAsync()).ToArray();

        if (incrementalOnly)
        {
            dataSources = dataSources
                .Where(dataSource => string.Equals(
                    AIDataSourceExtensions.GetSourceType(dataSource),
                    AIDataSourceSourceTypes.SearchIndexProfile,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (string.IsNullOrWhiteSpace(sourceIndexProfileName))
        {
            return dataSources;
        }

        return dataSources
            .Where(dataSource => string.Equals(dataSource.SourceIndexProfileName, sourceIndexProfileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
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

                AICoreLog.DataSourceSyncRetrying(_logger, ex, dataSource.ItemId.SanitizeLogValue(), attempt + 1, MaxRetries + 1, delay.TotalSeconds);

                // The attempt about to be made, starting at 2 for the first retry.
                RetryTelemetry.RecordRetry(TelemetryArea, CrestAppsOrchardCoreTelemetry.GetOutcome(ex), attempt + 2);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task SyncDataSourceAsync(
        AIDataSource dataSource,
        IEnumerable<IndexProfile> masterIndexProfiles,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) || !HasSourceConfiguration(dataSource))
        {
            return;
        }

        var masterProfile = masterIndexProfiles.FirstOrDefault(p =>
        string.Equals(p.Name, dataSource.AIKnowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));

        if (masterProfile == null)
        {
            return;
        }

        // Acquire a distributed lock scoped to this data source to prevent concurrent indexing.
        var (locker, isLocked) = await _distributedLock.TryAcquireMeasuredLockAsync(
            $"DataSourceIndexing-{dataSource.ItemId}",
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(60),
            TelemetryArea);

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
        var progress = new SyncProgress();
        using var telemetry = StartSyncTelemetry(dataSource, masterProfile, AIModuleTelemetry.ModeFull);

        try
        {
            await SyncDataSourceDocumentsAsync(dataSource, masterProfile, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);
            RecordSyncedDocuments(masterProfile, AIModuleTelemetry.ModeFull, progress);

            throw;
        }

        CompleteSyncTelemetry(telemetry, masterProfile, AIModuleTelemetry.ModeFull, progress, cancellationToken);

        if (!progress.Started)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var dataSourceId = dataSource.ItemId.SanitizeLogValue();
            var indexName = masterProfile.IndexName.SanitizeLogValue();

            AICoreLog.DataSourceSynced(
                _logger,
                progress.Chunks,
                progress.Indexed,
                dataSourceId,
                indexName,
                (long)telemetry.Elapsed.TotalMilliseconds,
                progress.Failed);
        }
    }

    private async Task SyncDataSourceDocumentsAsync(AIDataSource dataSource, IndexProfile masterProfile, SyncProgress progress, CancellationToken cancellationToken)
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

        var deployment = await ResolveEmbeddingDeploymentAsync(masterProfile, cancellationToken);

        if (deployment == null)
        {
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(deployment, builder => builder.UseDefaultResilience().UseUsageLabels(purpose: AIUsagePurposes.Indexing));

        if (embeddingGenerator == null)
        {
            _logger.LogWarning(
                "Embedding generator could not be created for deployment '{DeploymentName}' on master index '{IndexName}'.",
                deployment.Name,
                masterProfile.IndexName);

            return;
        }

        var sourceHandler = ResolveSourceHandler(dataSource);

        if (sourceHandler == null)
        {
            _logger.LogWarning(
                "No source handler found for source type '{SourceType}' on data source '{DataSourceId}'.",
                AIDataSourceExtensions.GetSourceType(dataSource),
                dataSource.ItemId);

            return;
        }

        progress.Started = true;

        var sourceDocuments = sourceHandler.ReadAsync(dataSource, cancellationToken);
        var referenceType = await sourceHandler.GetReferenceTypeAsync(dataSource, cancellationToken);

        var documents = new List<DocumentIndex>();

        var timestamp = _clock.UtcNow;

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

            // Normalize content and title, then chunk using token-aware splitter.

            sourceDoc.Title = _textNormalizer.NormalizeTitle(sourceDoc.Title);
            var chunkTexts = await _textNormalizer.NormalizeAndChunkAsync(sourceDoc.Content, cancellationToken);

            if (chunkTexts.Count == 0)
            {
                continue;
            }

            // Prepend the title to the first chunk so it is embedded and becomes searchable.
            if (!string.IsNullOrWhiteSpace(sourceDoc.Title))
            {
                chunkTexts[0] = sourceDoc.Title + "\n" + chunkTexts[0];
            }

            // Generate embeddings for all chunks.
            try
            {
                var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);

                if (embeddings == null || embeddings.Count != chunkTexts.Count)
                {
                    _logger.LogWarning("Failed to generate embeddings for document '{ReferenceId}' in data source '{DataSourceId}'.",
                    referenceId, dataSource.ItemId);

                    progress.Failed++;

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
                        ReferenceType = referenceType,
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
                    progress.Chunks++;

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

                            progress.WriteFailed = true;
                        }

                        documents.Clear();
                    }
                }

                progress.Indexed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document '{ReferenceId}' for data source '{DataSourceId}'. Continuing with remaining documents.",
                referenceId, dataSource.ItemId);

                progress.Failed++;
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

                progress.WriteFailed = true;
            }
        }
    }

    private static TelemetryScope StartSyncTelemetry(AIDataSource dataSource, IndexProfile masterProfile, string mode)
    {
        var telemetry = TelemetryScope.Start(
            AIModuleTelemetry.ActivitySource,
            AIModuleTelemetry.DataSourceSyncSpanName,
            AIModuleTelemetry.DataSourceSyncDuration,
            new TagList
            {
                { AIModuleTelemetry.ProviderTag, masterProfile.ProviderName ?? CrestAppsOrchardCoreTelemetry.Unknown },
                { AIModuleTelemetry.ModeTag, mode },
            });

        // Ids and index names go on the span only; on a metric they would be unbounded.
        telemetry.SetSpanTag(AITelemetry.DataSourceIdAttribute, dataSource.ItemId);
        telemetry.SetSpanTag("crestapps.index.name", masterProfile.IndexName);

        return telemetry;
    }

    private static void CompleteSyncTelemetry(TelemetryScope telemetry, IndexProfile masterProfile, string mode, SyncProgress progress, CancellationToken cancellationToken)
    {
        if (!progress.Started)
        {
            telemetry.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeSkipped);
        }
        else if (cancellationToken.IsCancellationRequested)
        {
            telemetry.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeCancelled);
        }
        else if (progress.Failed > 0 || progress.WriteFailed)
        {
            // Documents were lost on the way, though the sync went on.
            telemetry.SetOutcome(AIModuleTelemetry.OutcomePartial);
        }
        else
        {
            telemetry.Succeed();
        }

        telemetry.SetSpanTag("crestapps.data_source.documents.indexed", progress.Indexed);
        telemetry.SetSpanTag("crestapps.data_source.documents.failed", progress.Failed);

        RecordSyncedDocuments(masterProfile, mode, progress);
    }

    private static void RecordSyncedDocuments(IndexProfile masterProfile, string mode, SyncProgress progress)
    {
        AIModuleTelemetry.RecordDocuments(progress.Indexed, masterProfile.ProviderName, mode, AIModuleTelemetry.ActionIndexed);
        AIModuleTelemetry.RecordDocuments(progress.Failed, masterProfile.ProviderName, mode, AIModuleTelemetry.ActionFailed);
    }

    private async Task<AIDeployment> ResolveEmbeddingDeploymentAsync(
        IndexProfile masterProfile,
        CancellationToken cancellationToken)
    {
        var resolvedProfile = await ReloadProfileWithMetadataAsync(masterProfile);
        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(resolvedProfile);
        var embeddingDeploymentName = metadata.GetEmbeddingDeploymentName();

        if (string.IsNullOrWhiteSpace(embeddingDeploymentName))
        {
            _logger.LogWarning(
                "Embedding deployment is missing for master index '{IndexName}'.",
                resolvedProfile.IndexName);

            return null;
        }

        var configuredDeployment = await _deploymentManager.FindByNameAsync(embeddingDeploymentName, cancellationToken);

        if (configuredDeployment is not null && SupportsEmbedding(configuredDeployment))
        {
            return configuredDeployment;
        }

        var canonicalDeployment = await ResolveCanonicalEmbeddingDeploymentAsync(
            resolvedProfile,
            embeddingDeploymentName,
            cancellationToken);

        if (canonicalDeployment != null)
        {
            return canonicalDeployment;
        }

        _logger.LogWarning(
            "Configured embedding deployment '{DeploymentName}' was not found for master index '{IndexName}'.",
            embeddingDeploymentName,
            resolvedProfile.IndexName);

        return null;
    }

    private async Task<AIDeployment> ResolveCanonicalEmbeddingDeploymentAsync(
        IndexProfile resolvedProfile,
        string embeddingDeploymentName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedProfile);

        var selectorCandidates = GetEmbeddingSelectorCandidates(embeddingDeploymentName);

        if (selectorCandidates.Count == 0)
        {
            return null;
        }

        var deployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Embedding, clientName: null, cancellationToken) ?? [])
            .Where(deployment => MatchesEmbeddingSelector(deployment, selectorCandidates))
            .ToArray();

        if (deployments.Length != 1)
        {
            if (deployments.Length > 1)
            {
                _logger.LogWarning(
                    "Configured embedding deployment selector '{DeploymentName}' matched multiple embedding deployments for master index '{IndexName}'. Matches: {DeploymentMatches}.",
                    embeddingDeploymentName,
                    resolvedProfile.IndexName,
                    string.Join(", ", deployments.Select(deployment => deployment.Name)));
            }

            return null;
        }

        var resolvedDeployment = deployments[0];
        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(resolvedProfile);

        if (!string.Equals(metadata.GetEmbeddingDeploymentName(), resolvedDeployment.Name, StringComparison.OrdinalIgnoreCase))
        {
            metadata.SetEmbeddingDeploymentName(resolvedDeployment.Name);
            IndexProfileEmbeddingMetadataAccessor.StoreMetadata(resolvedProfile, metadata);
            await _indexProfileManager.UpdateAsync(resolvedProfile);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Resolved embedding deployment selector '{DeploymentName}' to deployment '{ResolvedDeploymentName}' for master index '{IndexName}'.",
                embeddingDeploymentName,
                resolvedDeployment.Name,
                resolvedProfile.IndexName);
        }

        return resolvedDeployment;
    }

    private async Task<IndexProfile> ReloadProfileWithMetadataAsync(IndexProfile masterProfile)
    {
        ArgumentNullException.ThrowIfNull(masterProfile);

        if (HasEmbeddingDeployment(masterProfile))
        {
            return masterProfile;
        }

        IndexProfile reloadedProfile = null;

        if (!string.IsNullOrWhiteSpace(masterProfile.Id))
        {
            reloadedProfile = await _indexProfileManager.FindByIdAsync(masterProfile.Id);
        }

        if (reloadedProfile == null &&
            !string.IsNullOrWhiteSpace(masterProfile.Name))
        {
            reloadedProfile = await _indexProfileManager.FindByNameAsync(masterProfile.Name);
        }

        return reloadedProfile ?? masterProfile;
    }

    private async Task<IndexProfile> ResolveMasterProfileAsync(
        string knowledgeBaseIndexProfileName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeBaseIndexProfileName);
        _ = cancellationToken;

        var masterProfile = await _indexProfileManager.FindByNameAsync(knowledgeBaseIndexProfileName);

        if (masterProfile != null)
        {
            return masterProfile;
        }

        return (await GetMasterIndexProfilesAsync())
            .FirstOrDefault(profile => string.Equals(profile.Name, knowledgeBaseIndexProfileName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSourceConfiguration(AIDataSource dataSource)
    {
        var sourceType = AIDataSourceExtensions.GetSourceType(dataSource);

        return !string.IsNullOrWhiteSpace(sourceType) &&
            (!string.Equals(sourceType, AIDataSourceSourceTypes.SearchIndexProfile, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName));
    }

    private IAIDataSourceSourceHandler ResolveSourceHandler(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var sourceType = AIDataSourceExtensions.GetSourceType(dataSource);

        return _serviceProvider.GetKeyedService<IAIDataSourceSourceHandler>(sourceType);
    }

    private static bool HasEmbeddingDeployment(IndexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return !string.IsNullOrWhiteSpace(
            IndexProfileEmbeddingMetadataAccessor.GetMetadata(profile).GetEmbeddingDeploymentName());
    }

    private async Task<List<IndexProfile>> GetMasterIndexProfilesAsync()
    {
        return (await _indexProfileManager.GetAllAsync())
            .Where(profile => string.Equals(profile.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool MatchesEmbeddingSelector(AIDeployment deployment, HashSet<string> selectorCandidates)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(selectorCandidates);

        return SupportsEmbedding(deployment) &&
            (selectorCandidates.Contains(deployment.Name) ||
                selectorCandidates.Contains(deployment.ModelName));
    }

    // textEmbedding is opt-in, so a deployment that declares no capability metadata does not qualify --
    // an embedding endpoint has to say so. A record still carrying the legacy Embedding purpose has already
    // been projected onto this feature by the time it is read.
    private static bool SupportsEmbedding(AIDeployment deployment)
        => deployment.TryGet<AIDeploymentMetadata>(out var metadata) &&
            metadata.SupportsFeature(AIDeploymentFeatureNames.TextEmbedding);

    private static HashSet<string> GetEmbeddingSelectorCandidates(string embeddingDeploymentName)
    {
        if (string.IsNullOrWhiteSpace(embeddingDeploymentName))
        {
            return [];
        }

        var selectorCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            embeddingDeploymentName.Trim(),
        };

        foreach (var prefix in new[] { "Azure-", "AzureOpenAI-", "AzureOpenAIOwnData-" })
        {
            if (embeddingDeploymentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                selectorCandidates.Add(embeddingDeploymentName.Substring(prefix.Length));
            }
        }

        return selectorCandidates;
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

    /// <summary>
    /// What one sync did, for its telemetry and its summary log.
    /// </summary>
    private sealed class SyncProgress
    {
        /// <summary>
        /// Gets or sets a value indicating whether the sync got as far as reading the source documents.
        /// </summary>
        public bool Started { get; set; }

        /// <summary>
        /// Gets or sets the source documents written to the index.
        /// </summary>
        public int Indexed { get; set; }

        /// <summary>
        /// Gets or sets the source documents that could not be embedded or prepared, and were skipped.
        /// </summary>
        public int Failed { get; set; }

        /// <summary>
        /// Gets or sets the chunks prepared for the index.
        /// </summary>
        public int Chunks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a batch of chunks could not be written to the index.
        /// </summary>
        public bool WriteFailed { get; set; }
    }
}
