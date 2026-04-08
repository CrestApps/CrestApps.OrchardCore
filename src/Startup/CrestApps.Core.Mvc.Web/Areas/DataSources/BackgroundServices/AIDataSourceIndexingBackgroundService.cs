using CrestApps.Core.AI.Services;
using CrestApps.Core.Mvc.Web.Areas.DataSources.Services;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.BackgroundServices;

public sealed class AIDataSourceIndexingBackgroundService : BackgroundService
{
    private readonly MvcAIDataSourceIndexingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIDataSourceIndexingBackgroundService> _logger;

    public AIDataSourceIndexingBackgroundService(
        MvcAIDataSourceIndexingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AIDataSourceIndexingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var indexingService = scope.ServiceProvider.GetRequiredService<IAIDataSourceIndexingService>();

                switch (workItem.Type)
                {
                    case MvcAIDataSourceIndexingWorkItemType.SyncDataSource:
                        await indexingService.SyncDataSourceAsync(workItem.DataSource, stoppingToken);
                        break;
                    case MvcAIDataSourceIndexingWorkItemType.DeleteDataSource:
                        await indexingService.DeleteDataSourceDocumentsAsync(workItem.DataSource, stoppingToken);
                        break;
                    case MvcAIDataSourceIndexingWorkItemType.SyncSourceDocuments:
                        await indexingService.SyncSourceDocumentsAsync(workItem.DocumentIds, stoppingToken);
                        break;
                    case MvcAIDataSourceIndexingWorkItemType.RemoveSourceDocuments:
                        await indexingService.RemoveSourceDocumentsAsync(workItem.DocumentIds, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing queued data source indexing work.");
            }
        }
    }
}
