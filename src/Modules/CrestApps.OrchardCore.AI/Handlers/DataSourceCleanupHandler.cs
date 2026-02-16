using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Handles data source deletion by cleaning up associated documents from the master embedding index.
/// </summary>
internal sealed class DataSourceCleanupHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly DataSourceIndexingService _indexingService;
    private readonly ILogger _logger;

    public DataSourceCleanupHandler(
        DataSourceIndexingService indexingService,
        ILogger<DataSourceCleanupHandler> logger)
    {
        _indexingService = indexingService;
        _logger = logger;
    }

    public override async Task DeletedAsync(DeletedContext<AIDataSource> context)
    {
        try
        {
            await _indexingService.DeleteDataSourceDocumentsAsync(context.Model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up master index documents for deleted data source '{DataSourceId}'.",
                context.Model.ItemId);
        }
    }
}
