using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.Services;
using CrestApps.Core.Mvc.Web.Areas.DataSources.Services;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Handlers;

public sealed class ArticleIndexingHandler : CatalogEntryHandlerBase<Article>
{
    private readonly ArticleIndexingService _indexingService;
    private readonly IMvcAIDataSourceIndexingQueue _dataSourceIndexingQueue;
    private readonly ILogger<ArticleIndexingHandler> _logger;

    public ArticleIndexingHandler(
        ArticleIndexingService indexingService,
        IMvcAIDataSourceIndexingQueue dataSourceIndexingQueue,
        ILogger<ArticleIndexingHandler> logger)
    {
        _indexingService = indexingService;
        _dataSourceIndexingQueue = dataSourceIndexingQueue;
        _logger = logger;
    }

    public override async Task CreatedAsync(CreatedContext<Article> context)
    {
        try
        {
            await _indexingService.IndexAsync(context.Model);
            await _dataSourceIndexingQueue.QueueSyncSourceDocumentsAsync([context.Model.ItemId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index article '{ArticleId}' after creation.", context.Model.ItemId);
        }
    }

    public override async Task UpdatedAsync(UpdatedContext<Article> context)
    {
        try
        {
            await _indexingService.IndexAsync(context.Model);
            await _dataSourceIndexingQueue.QueueSyncSourceDocumentsAsync([context.Model.ItemId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-index article '{ArticleId}' after update.", context.Model.ItemId);
        }
    }

    public override async Task DeletedAsync(DeletedContext<Article> context)
    {
        try
        {
            await _indexingService.DeleteAsync(context.Model.ItemId);
            await _dataSourceIndexingQueue.QueueRemoveSourceDocumentsAsync([context.Model.ItemId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove article '{ArticleId}' from search index after deletion.", context.Model.ItemId);
        }
    }
}
