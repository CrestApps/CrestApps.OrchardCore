using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Handles data source lifecycle events: triggers indexing on creation and cleans up on deletion.
/// </summary>
internal sealed class DataSourceIndexingHandler : CatalogEntryHandlerBase<AIDataSource>
{
    private readonly DataSourceIndexingService _indexingService;
    private readonly ILogger _logger;

    public DataSourceIndexingHandler(
        DataSourceIndexingService indexingService,
        ILogger<DataSourceIndexingHandler> logger)
    {
        _indexingService = indexingService;
        _logger = logger;
    }

    public override async Task CreatedAsync(CreatedContext<AIDataSource> context)
    {
        try
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("process-datasource-sync", context.Model, async (scope, ds) =>
            {
                var indexingService = scope.ServiceProvider.GetRequiredService<DataSourceIndexingService>();

                await indexingService.SyncDataSourceAsync(ds);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering initial indexing for data source '{DataSourceId}'.",
                context.Model.ItemId);
        }
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
