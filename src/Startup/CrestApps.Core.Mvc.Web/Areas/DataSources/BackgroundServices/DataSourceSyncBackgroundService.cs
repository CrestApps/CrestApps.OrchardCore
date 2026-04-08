using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.BackgroundServices;


/// <summary>
/// Periodically synchronizes AI data source documents with their configured search indexes.
/// Runs every 5 minutes to detect new, updated, or removed data source documents.
/// </summary>
public sealed class DataSourceSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataSourceSyncBackgroundService> _logger;

    public DataSourceSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataSourceSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                await using var scope = _scopeFactory.CreateAsyncScope();
                await SyncDataSourcesAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while synchronizing AI data sources.");
            }
        }
    }
    /// <summary>
    /// Synchronizes all configured data sources with their target indexes.
    /// </summary>
    private async Task SyncDataSourcesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dataSourceStore = services.GetService<IAIDataSourceStore>();
        var indexingService = services.GetService<IAIDataSourceIndexingService>();

        if (dataSourceStore == null || indexingService == null)
        {
            _logger.LogDebug("Data source synchronization is not fully configured. Skipping sync.");

            return;
        }

        var dataSources = await dataSourceStore.GetAllAsync();
        var dataSourceList = dataSources?.ToList() ?? [];

        if (dataSourceList.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting data source sync for {Count} data source(s).", dataSourceList.Count);
        }

        await indexingService.SyncAllAsync(cancellationToken);
    }
}
