using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Handles index profile synchronization events for data source master indexes.
/// When a master index is synchronized (e.g., created or reset), triggers a full re-sync
/// of all data source documents into the master embedding index.
/// </summary>
public sealed class DataSourceIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDataSourceIndexingQueue _indexingQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="indexingQueue">The indexing queue.</param>
    public DataSourceIndexProfileHandler(
        IAIDataSourceStore dataSourceStore,
        IAIDataSourceIndexingQueue indexingQueue)
    {
        _dataSourceStore = dataSourceStore;
        _indexingQueue = indexingQueue;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (!string.Equals(DataSourceConstants.IndexingTaskType, context.IndexProfile.Type, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var dataSource in await _dataSourceStore.GetAllAsync())
        {
            if (!string.Equals(dataSource.AIKnowledgeBaseIndexProfileName, context.IndexProfile.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await _indexingQueue.QueueSyncDataSourceAsync(dataSource);
        }
    }
}
