using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.AI.DataSources;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

internal sealed class DataSourceSourceIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAIDataSourceIndexingQueue _indexingQueue;

    public DataSourceSourceIndexProfileHandler(
        IAIDataSourceStore dataSourceStore,
        IAIDataSourceIndexingQueue indexingQueue)
    {
        _dataSourceStore = dataSourceStore;
        _indexingQueue = indexingQueue;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (string.IsNullOrWhiteSpace(context.IndexProfile.Name) ||
            string.Equals(DataSourceConstants.IndexingTaskType, context.IndexProfile.Type, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var dataSource in await _dataSourceStore.GetAllAsync())
        {
            if (!string.Equals(dataSource.SourceIndexProfileName, context.IndexProfile.Name, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(dataSource.AIKnowledgeBaseIndexProfileName))
            {
                continue;
            }

            await _indexingQueue.QueueSyncDataSourceAsync(dataSource);
        }
    }
}
