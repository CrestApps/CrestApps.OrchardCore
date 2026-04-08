using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Core.Mvc.Web.Areas.Indexing.Services;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.BackgroundServices;

public sealed class AIChatDocumentIndexingBackgroundService : BackgroundService
{
    private readonly MvcAIChatDocumentIndexingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AIChatDocumentIndexingBackgroundService> _logger;

    public AIChatDocumentIndexingBackgroundService(
        MvcAIChatDocumentIndexingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AIChatDocumentIndexingBackgroundService> logger)
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
                var indexingService = scope.ServiceProvider.GetRequiredService<MvcAIDocumentIndexingService>();

                switch (workItem.Type)
                {
                    case MvcAIChatDocumentIndexingWorkItemType.Index:
                        await indexingService.IndexAsync(workItem.Document, workItem.Chunks, stoppingToken);
                        break;
                    case MvcAIChatDocumentIndexingWorkItemType.DeleteChunks:
                        await indexingService.DeleteChunksAsync(workItem.ChunkIds, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing queued MVC chat document indexing work.");
            }
        }
    }
}
