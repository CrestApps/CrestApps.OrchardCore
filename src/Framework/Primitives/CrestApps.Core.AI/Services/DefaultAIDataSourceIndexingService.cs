using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Services;

public sealed class DefaultAIDataSourceIndexingService : IAIDataSourceIndexingService
{
    private const int BatchSize = 250;
    private const int MaxChunkIdsPerDocument = 1000;

    private readonly ICatalog<AIDataSource> _dataSourceCatalog;
    private readonly ISearchIndexProfileManager _indexProfileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IAITextNormalizer _textNormalizer;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultAIDataSourceIndexingService> _logger;

    public DefaultAIDataSourceIndexingService(
        ICatalog<AIDataSource> dataSourceCatalog,
        ISearchIndexProfileManager indexProfileManager,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IAITextNormalizer textNormalizer,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<DefaultAIDataSourceIndexingService> logger)
    {
        _dataSourceCatalog = dataSourceCatalog;
        _indexProfileManager = indexProfileManager;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _textNormalizer = textNormalizer;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var dataSources = await _dataSourceCatalog.GetAllAsync();

        foreach (var dataSource in dataSources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SyncDataSourceAsync(dataSource, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synchronize data source '{DataSourceId}'.", dataSource.ItemId);
            }
        }
    }

    public async Task SyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var context = await TryCreateContextAsync(dataSource, requireSourceProfile: true);

        if (context == null)
        {
            return;
        }

        await EnsureKnowledgeBaseIndexAsync(context, cancellationToken);
        await context.ContentManager.DeleteByDataSourceIdAsync(context.KnowledgeBaseProfile, dataSource.ItemId, cancellationToken);

        var sourceDocuments = context.DocumentReader.ReadAsync(
            context.SourceProfile,
            dataSource.KeyFieldName,
            dataSource.TitleFieldName,
            dataSource.ContentFieldName,
            cancellationToken);

        await IndexDocumentsAsync(context, sourceDocuments, deleteExistingChunks: false, cancellationToken);
    }

    public async Task SyncSourceDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        var ids = NormalizeDocumentIds(documentIds);

        if (ids.Length == 0)
        {
            return;
        }

        var dataSources = await _dataSourceCatalog.GetAllAsync();

        foreach (var dataSource in dataSources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = await TryCreateContextAsync(dataSource, requireSourceProfile: true);

            if (context == null)
            {
                continue;
            }

            await EnsureKnowledgeBaseIndexAsync(context, cancellationToken);

            var sourceDocuments = context.DocumentReader.ReadByIdsAsync(
                context.SourceProfile,
                ids,
                dataSource.KeyFieldName,
                dataSource.TitleFieldName,
                dataSource.ContentFieldName,
                cancellationToken);

            await IndexDocumentsAsync(context, sourceDocuments, deleteExistingChunks: true, cancellationToken);
        }
    }

    public async Task RemoveSourceDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        var ids = NormalizeDocumentIds(documentIds);

        if (ids.Length == 0)
        {
            return;
        }

        var chunkIds = BuildChunkIds(ids);
        var dataSources = await _dataSourceCatalog.GetAllAsync();

        foreach (var dataSource in dataSources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = await TryCreateContextAsync(dataSource, requireSourceProfile: false);

            if (context == null)
            {
                continue;
            }

            await context.DocumentManager.DeleteAsync(context.KnowledgeBaseProfile, chunkIds, cancellationToken);
        }
    }

    public async Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var context = await TryCreateContextAsync(dataSource, requireSourceProfile: false);

        if (context == null)
        {
            return;
        }

        await context.ContentManager.DeleteByDataSourceIdAsync(context.KnowledgeBaseProfile, dataSource.ItemId, cancellationToken);
    }

    private async Task IndexDocumentsAsync(
        DataSourceIndexingContext context,
        IAsyncEnumerable<KeyValuePair<string, SourceDocument>> sourceDocuments,
        bool deleteExistingChunks,
        CancellationToken cancellationToken)
    {
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;
        var indexedChunkCount = 0;
        var documents = new List<IndexDocument>();

        await foreach (var pair in sourceDocuments.WithCancellation(cancellationToken))
        {
            var referenceId = pair.Key;
            var sourceDocument = pair.Value;

            if (string.IsNullOrWhiteSpace(referenceId) || string.IsNullOrWhiteSpace(sourceDocument?.Content))
            {
                continue;
            }

            var normalizedTitle = _textNormalizer.NormalizeTitle(sourceDocument.Title);
            var chunkTexts = await _textNormalizer.NormalizeAndChunkAsync(sourceDocument.Content, cancellationToken);

            if (chunkTexts.Count == 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                chunkTexts[0] = normalizedTitle + "\n" + chunkTexts[0];
            }

            var embeddings = await context.EmbeddingGenerator.GenerateAsync(chunkTexts, cancellationToken: cancellationToken);

            if (embeddings == null || embeddings.Count != chunkTexts.Count)
            {
                _logger.LogWarning(
                    "Skipping document '{ReferenceId}' for data source '{DataSourceId}' because embeddings could not be generated.",
                    referenceId,
                    context.DataSource.ItemId);

                continue;
            }

            if (deleteExistingChunks)
            {
                await context.DocumentManager.DeleteAsync(
                    context.KnowledgeBaseProfile,
                    BuildChunkIds([referenceId]),
                    cancellationToken);
            }

            var filters = BuildFilterFields(sourceDocument.Fields);

            for (var i = 0; i < chunkTexts.Count; i++)
            {
                var chunkId = $"{referenceId}_{i}";
                var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [DataSourceConstants.ColumnNames.ChunkId] = chunkId,
                    [DataSourceConstants.ColumnNames.ReferenceId] = referenceId,
                    [DataSourceConstants.ColumnNames.DataSourceId] = context.DataSource.ItemId,
                    [DataSourceConstants.ColumnNames.ReferenceType] = context.SourceProfile.Type,
                    [DataSourceConstants.ColumnNames.ChunkIndex] = i,
                    [DataSourceConstants.ColumnNames.Title] = normalizedTitle,
                    [DataSourceConstants.ColumnNames.Content] = chunkTexts[i],
                    [DataSourceConstants.ColumnNames.Embedding] = embeddings[i].Vector.ToArray(),
                    [DataSourceConstants.ColumnNames.Timestamp] = timestamp,
                };

                if (filters != null)
                {
                    fields[DataSourceConstants.ColumnNames.Filters] = filters;
                }

                documents.Add(new IndexDocument
                {
                    Id = chunkId,
                    Fields = fields,
                });

                indexedChunkCount++;

                if (documents.Count >= BatchSize)
                {
                    await FlushAsync(context, documents, cancellationToken);
                }
            }
        }

        await FlushAsync(context, documents, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Synchronized {ChunkCount} knowledge-base chunk(s) for data source '{DataSourceId}'.",
                indexedChunkCount,
                context.DataSource.ItemId);
        }
    }

    private async Task FlushAsync(
        DataSourceIndexingContext context,
        List<IndexDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return;
        }

        var success = await context.DocumentManager.AddOrUpdateAsync(
            context.KnowledgeBaseProfile,
            documents.ToArray(),
            cancellationToken);

        if (!success)
        {
            _logger.LogWarning(
                "Knowledge-base indexing reported a failure for data source '{DataSourceId}' in index '{IndexName}'.",
                context.DataSource.ItemId,
                context.KnowledgeBaseProfile.IndexFullName);
        }

        documents.Clear();
    }

    private async Task EnsureKnowledgeBaseIndexAsync(
        DataSourceIndexingContext context,
        CancellationToken cancellationToken)
    {
        context.KnowledgeBaseProfile.IndexFullName ??= context.IndexManager.ComposeIndexFullName(context.KnowledgeBaseProfile);

        if (await context.IndexManager.ExistsAsync(context.KnowledgeBaseProfile, cancellationToken))
        {
            return;
        }

        var fields = await _indexProfileManager.GetFieldsAsync(context.KnowledgeBaseProfile, cancellationToken);

        if (fields == null || fields.Count == 0)
        {
            throw new InvalidOperationException(
                $"The knowledge-base index profile '{context.KnowledgeBaseProfile.Name}' does not expose a schema.");
        }

        await context.IndexManager.CreateAsync(context.KnowledgeBaseProfile, fields, cancellationToken);
    }

    private async Task<DataSourceIndexingContext> TryCreateContextAsync(AIDataSource dataSource, bool requireSourceProfile)
    {
        if (string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            return null;
        }

        var knowledgeBaseProfile = await _indexProfileManager.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

        if (knowledgeBaseProfile == null)
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because knowledge-base index profile '{IndexProfileName}' was not found.",
                dataSource.ItemId,
                dataSource.AIKnowledgeBaseIndexProfileName);

            return null;
        }

        if (!string.Equals(knowledgeBaseProfile.Type, IndexProfileTypes.DataSource, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because knowledge-base index profile '{IndexProfileName}' is not a data-source profile.",
                dataSource.ItemId,
                knowledgeBaseProfile.Name);

            return null;
        }

        var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(knowledgeBaseProfile.ProviderName);
        var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(knowledgeBaseProfile.ProviderName);
        var contentManager = _serviceProvider.GetKeyedService<IDataSourceContentManager>(knowledgeBaseProfile.ProviderName);

        if (indexManager == null || documentManager == null || contentManager == null)
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because provider '{ProviderName}' is not fully configured for data-source indexing.",
                dataSource.ItemId,
                knowledgeBaseProfile.ProviderName);

            return null;
        }

        if (!requireSourceProfile)
        {
            return new DataSourceIndexingContext(
                dataSource,
                knowledgeBaseProfile,
                null,
                indexManager,
                documentManager,
                contentManager,
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName))
        {
            return null;
        }

        var sourceProfile = await _indexProfileManager.FindByNameAsync(dataSource.SourceIndexProfileName);

        if (sourceProfile == null)
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because source index profile '{IndexProfileName}' was not found.",
                dataSource.ItemId,
                dataSource.SourceIndexProfileName);

            return null;
        }

        var documentReader = _serviceProvider.GetKeyedService<IDataSourceDocumentReader>(sourceProfile.ProviderName);

        if (documentReader == null)
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because provider '{ProviderName}' cannot read source documents.",
                dataSource.ItemId,
                sourceProfile.ProviderName);

            return null;
        }

        var profileMetadata = SearchIndexProfileEmbeddingMetadataAccessor.GetMetadata(knowledgeBaseProfile);
        var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
            _deploymentManager,
            _aiClientFactory,
            profileMetadata);

        if (embeddingGenerator == null)
        {
            _logger.LogWarning(
                "Skipping data source '{DataSourceId}' because knowledge-base index '{IndexProfileName}' has no embedding deployment configured.",
                dataSource.ItemId,
                knowledgeBaseProfile.Name);

            return null;
        }

        return new DataSourceIndexingContext(
            dataSource,
            knowledgeBaseProfile,
            sourceProfile,
            indexManager,
            documentManager,
            contentManager,
            documentReader,
            embeddingGenerator);
    }

    private static string[] NormalizeDocumentIds(IEnumerable<string> documentIds)
        => documentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static List<string> BuildChunkIds(IEnumerable<string> referenceIds)
    {
        var chunkIds = new List<string>();

        foreach (var referenceId in referenceIds)
        {
            for (var i = 0; i < MaxChunkIdsPerDocument; i++)
            {
                chunkIds.Add($"{referenceId}_{i}");
            }
        }

        return chunkIds;
    }

    private static Dictionary<string, object> BuildFilterFields(Dictionary<string, object> sourceFields)
    {
        if (sourceFields == null || sourceFields.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object>(sourceFields, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record DataSourceIndexingContext(
        AIDataSource DataSource,
        SearchIndexProfile KnowledgeBaseProfile,
        SearchIndexProfile SourceProfile,
        ISearchIndexManager IndexManager,
        ISearchDocumentManager DocumentManager,
        IDataSourceContentManager ContentManager,
        IDataSourceDocumentReader DocumentReader,
        IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator);
}
