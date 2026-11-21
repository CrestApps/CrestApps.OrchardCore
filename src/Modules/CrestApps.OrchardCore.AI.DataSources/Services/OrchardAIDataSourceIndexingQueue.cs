using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class OrchardAIDataSourceIndexingQueue : IAIDataSourceIndexingQueue
{
    private readonly List<OrchardAIDataSourceIndexingWorkItem> _workItems = [];

    private readonly ILogger _logger;
    private bool _taskAdded;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardAIDataSourceIndexingQueue"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OrchardAIDataSourceIndexingQueue(ILogger<OrchardAIDataSourceIndexingQueue> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously performs the queue sync data source operation.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueSyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var workItem = OrchardAIDataSourceIndexingWorkItem.ForSyncDataSource(dataSource.Clone());

        QueueWorkItem(workItem, dataSource.ItemId, 0);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously performs the queue delete data source operation.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueDeleteDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var workItem = OrchardAIDataSourceIndexingWorkItem.ForDeleteDataSource(dataSource.Clone());

        QueueWorkItem(workItem, dataSource.ItemId, 0);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Asynchronously performs the queue sync source documents operation.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueSyncSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(sourceIndexProfileName, documentIds, OrchardAIDataSourceIndexingWorkItem.ForSyncSourceDocuments, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the queue remove source documents operation.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueRemoveSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(sourceIndexProfileName, documentIds, OrchardAIDataSourceIndexingWorkItem.ForRemoveSourceDocuments, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the queue sync data source documents operation.
    /// </summary>
    /// <param name="dataSourceId">The data source identifier.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueSyncDataSourceDocumentsAsync(string dataSourceId, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(dataSourceId, documentIds, OrchardAIDataSourceIndexingWorkItem.ForSyncDataSourceDocuments, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs the queue remove data source documents operation.
    /// </summary>
    /// <param name="dataSourceId">The data source identifier.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public ValueTask QueueRemoveDataSourceDocumentsAsync(string dataSourceId, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(dataSourceId, documentIds, OrchardAIDataSourceIndexingWorkItem.ForRemoveDataSourceDocuments, cancellationToken);
    }

    private ValueTask QueueDocumentIdsAsync(
        string sourceIndexProfileName,
        IReadOnlyCollection<string> documentIds,
        Func<string, IReadOnlyCollection<string>, OrchardAIDataSourceIndexingWorkItem> factory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIndexProfileName);
        ArgumentNullException.ThrowIfNull(documentIds);

        var ids = documentIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Skipped queueing Orchard AI data-source work item because no document ids remained after normalization.");
            }

            return ValueTask.CompletedTask;
        }

        var workItem = factory(sourceIndexProfileName, ids);

        QueueWorkItem(workItem, sourceIndexProfileName, ids.Length);

        return ValueTask.CompletedTask;
    }

    private void QueueWorkItem(OrchardAIDataSourceIndexingWorkItem workItem, string target, int documentCount)
    {
        _workItems.Add(workItem);
        AddDeferredTask();

        LogQueuedWorkItem(workItem, target, documentCount);
    }

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        var workItems = _workItems;

        // Captured while the request that queued the work is still running, so the indexing links back to it.
        var traceParent = TraceContextPropagation.Capture();

        ShellScope.AddDeferredTask(scope => ProcessAfterEndOfRequestAsync(scope, workItems, traceParent));
    }

    private static Task ProcessAfterEndOfRequestAsync(
        ShellScope shellScope,
        List<OrchardAIDataSourceIndexingWorkItem> workItems,
        string traceParent)
    {
        ArgumentNullException.ThrowIfNull(shellScope);
        ArgumentNullException.ThrowIfNull(workItems);

        var pendingWorkItems = workItems.ToArray();

        if (pendingWorkItems.Length == 0)
        {
            return Task.CompletedTask;
        }

        // The work runs in the background job, so that is what is measured, rather than the hand-off to it.
        return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            "process-ai-datasource-indexing",
            scope => DeferredWork.RunAsync("ai_datasource_indexing", traceParent, scope, s => ProcessAsync(s.ServiceProvider, pendingWorkItems)));
    }

    internal static async Task ProcessAsync(
        IServiceProvider services,
        OrchardAIDataSourceIndexingWorkItem[] workItems)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(workItems);

        if (workItems.Length == 0)
        {
            return;
        }

        var indexingService = services.GetRequiredService<IAIDataSourceIndexingService>();
        var logger = services.GetRequiredService<ILogger<OrchardAIDataSourceIndexingQueue>>();
        var synced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var workItem in workItems)
        {
            try
            {
                switch (workItem.Type)
                {
                    case OrchardAIDataSourceIndexingWorkItemType.SyncDataSource:
                        await SyncStoredDataSourceAsync(services, indexingService, workItem.DataSource.ItemId, synced);
                        break;
                    case OrchardAIDataSourceIndexingWorkItemType.DeleteDataSource:
                        await indexingService.DeleteDataSourceDocumentsAsync(workItem.DataSource);
                        break;
                    case OrchardAIDataSourceIndexingWorkItemType.SyncSourceDocuments:
                        await indexingService.SyncSourceDocumentsAsync(workItem.SourceIndexProfileName, workItem.DocumentIds);
                        break;
                    case OrchardAIDataSourceIndexingWorkItemType.RemoveSourceDocuments:
                        await indexingService.RemoveSourceDocumentsAsync(workItem.SourceIndexProfileName, workItem.DocumentIds);
                        break;
                    case OrchardAIDataSourceIndexingWorkItemType.SyncDataSourceDocuments:
                        await indexingService.SyncDataSourceDocumentsAsync(workItem.DataSourceId, workItem.DocumentIds);
                        break;
                    case OrchardAIDataSourceIndexingWorkItemType.RemoveDataSourceDocuments:
                        await indexingService.RemoveDataSourceDocumentsAsync(workItem.DataSourceId, workItem.DocumentIds);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing deferred AI data-source work item '{WorkItemType}'.", workItem.Type);
            }
        }
    }

    // The data source is read again rather than taken from the copy queued with the work: a request can save a data
    // source more than once, and only what it left stored may be synced (review 9, K1). Each one is synced once, since
    // every read returns the same record; one no longer stored is not synced.
    private static async Task SyncStoredDataSourceAsync(IServiceProvider services, IAIDataSourceIndexingService indexingService, string dataSourceId, HashSet<string> synced)
    {
        if (string.IsNullOrEmpty(dataSourceId) || !synced.Add(dataSourceId))
        {
            return;
        }

        var dataSource = await services.GetRequiredService<IAIDataSourceStore>().FindByIdAsync(dataSourceId);

        if (dataSource is not null)
        {
            await indexingService.SyncDataSourceAsync(dataSource);
        }
    }

    private void LogQueuedWorkItem(OrchardAIDataSourceIndexingWorkItem workItem, string target, int documentCount)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "Queued Orchard AI data-source work item {WorkItemType}. Target={Target}, DocumentCount={DocumentCount}.",
                workItem.Type,
                target,
                documentCount);
        }
    }
}
