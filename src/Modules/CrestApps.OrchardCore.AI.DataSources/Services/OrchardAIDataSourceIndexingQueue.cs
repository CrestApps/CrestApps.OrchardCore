using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
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

    public OrchardAIDataSourceIndexingQueue(ILogger<OrchardAIDataSourceIndexingQueue> logger)
    {
        _logger = logger;
    }

    public ValueTask QueueSyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var workItem = OrchardAIDataSourceIndexingWorkItem.ForSyncDataSource(dataSource.Clone());

        QueueWorkItem(workItem, dataSource.ItemId, 0);

        return ValueTask.CompletedTask;
    }

    public ValueTask QueueDeleteDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var workItem = OrchardAIDataSourceIndexingWorkItem.ForDeleteDataSource(dataSource.Clone());

        QueueWorkItem(workItem, dataSource.ItemId, 0);

        return ValueTask.CompletedTask;
    }

    public ValueTask QueueSyncSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(sourceIndexProfileName, documentIds, OrchardAIDataSourceIndexingWorkItem.ForSyncSourceDocuments, cancellationToken);
    }

    public ValueTask QueueRemoveSourceDocumentsAsync(string sourceIndexProfileName, IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
    {
        return QueueDocumentIdsAsync(sourceIndexProfileName, documentIds, OrchardAIDataSourceIndexingWorkItem.ForRemoveSourceDocuments, cancellationToken);
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

        ShellScope.AddDeferredTask(scope => ProcessAfterEndOfRequestAsync(scope, workItems));
    }

    private static Task ProcessAfterEndOfRequestAsync(
        ShellScope shellScope,
        List<OrchardAIDataSourceIndexingWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(shellScope);
        ArgumentNullException.ThrowIfNull(workItems);

        var pendingWorkItems = workItems.ToArray();

        if (pendingWorkItems.Length == 0)
        {
            return Task.CompletedTask;
        }

        return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            "process-ai-datasource-indexing",
            scope => ProcessAsync(scope, pendingWorkItems));
    }

    private static async Task ProcessAsync(
        ShellScope scope,
        OrchardAIDataSourceIndexingWorkItem[] workItems)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(workItems);

        if (workItems.Length == 0)
        {
            return;
        }

        var indexingService = scope.ServiceProvider.GetRequiredService<IAIDataSourceIndexingService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrchardAIDataSourceIndexingQueue>>();

        foreach (var workItem in workItems)
        {
            try
            {
                switch (workItem.Type)
                {
                    case OrchardAIDataSourceIndexingWorkItemType.SyncDataSource:
                        await indexingService.SyncDataSourceAsync(workItem.DataSource);
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
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing deferred AI data-source work item '{WorkItemType}'.", workItem.Type);
            }
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
