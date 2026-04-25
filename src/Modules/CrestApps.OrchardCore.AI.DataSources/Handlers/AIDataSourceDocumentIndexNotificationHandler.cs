using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

internal sealed class AIDataSourceDocumentIndexNotificationHandler : IDocumentIndexHandler
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDataSourceIndexingQueue _indexingQueue;
    private readonly ILogger _logger;

    public AIDataSourceDocumentIndexNotificationHandler(
        IAIDataSourceStore dataSourceStore,
        IAIDataSourceIndexingQueue indexingQueue,
        ILogger<AIDataSourceDocumentIndexNotificationHandler> logger)
    {
        _dataSourceStore = dataSourceStore;
        _indexingQueue = indexingQueue;
        _logger = logger;
    }

    public Task BuildIndexAsync(BuildDocumentIndexContext context) => Task.CompletedTask;

    public Task DocumentsAddedOrUpdatedAsync(
        IndexProfile indexProfile,
        IEnumerable<DocumentIndex> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(documents);

        return QueueAsync(indexProfile, documents.Select(static document => document.Id), isDelete: false, cancellationToken);
    }

    public Task DocumentsDeletedAsync(
        IndexProfile indexProfile,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(documentIds);

        return QueueAsync(indexProfile, documentIds, isDelete: true, cancellationToken);
    }

    private async Task QueueAsync(
        IndexProfile indexProfile,
        IEnumerable<string> documentIds,
        bool isDelete,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(documentIds);

        if (string.IsNullOrWhiteSpace(indexProfile.Name) ||
            string.Equals(indexProfile.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ids = documentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();

        var hasMapping = (await _dataSourceStore.GetAllAsync(cancellationToken))
            .Any(dataSource =>
                string.Equals(dataSource.SourceIndexProfileName, indexProfile.Name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName));

        if (!hasMapping)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "Queueing AI data-source synchronization for source index profile '{IndexProfileName}' with {DocumentCount} document id(s). IsDelete={IsDelete}.",
                indexProfile.Name,
                ids.Length,
                isDelete);
        }

        if (isDelete)
        {
            await _indexingQueue.QueueRemoveSourceDocumentsAsync(indexProfile.Name, ids, cancellationToken);
            return;
        }

        await _indexingQueue.QueueSyncSourceDocumentsAsync(indexProfile.Name, ids, cancellationToken);
    }
}
