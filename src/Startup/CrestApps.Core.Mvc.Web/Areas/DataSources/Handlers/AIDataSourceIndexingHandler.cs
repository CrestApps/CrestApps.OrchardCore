using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Mvc.Web.Areas.DataSources.Services;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.Handlers;

public sealed class AIDataSourceIndexingHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly IMvcAIDataSourceIndexingQueue _indexingQueue;
    private readonly ILogger<AIDataSourceIndexingHandler> _logger;

    public AIDataSourceIndexingHandler(
        IMvcAIDataSourceIndexingQueue indexingQueue,
        ILogger<AIDataSourceIndexingHandler> logger)
    {
        _indexingQueue = indexingQueue;
        _logger = logger;
    }

    public override async Task CreatedAsync(CreatedContext<AIDataSource> context)
    {
        try
        {
            await _indexingQueue.QueueSyncDataSourceAsync(context.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue initial indexing for data source '{DataSourceId}'.", context.Model.ItemId);
        }
    }

    public override async Task UpdatedAsync(UpdatedContext<AIDataSource> context)
    {
        try
        {
            await _indexingQueue.QueueSyncDataSourceAsync(context.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue synchronization for updated data source '{DataSourceId}'.", context.Model.ItemId);
        }
    }

    public override async Task DeletedAsync(DeletedContext<AIDataSource> context)
    {
        try
        {
            await _indexingQueue.QueueDeleteDataSourceAsync(context.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue cleanup for deleted data source '{DataSourceId}'.", context.Model.ItemId);
        }
    }
}
