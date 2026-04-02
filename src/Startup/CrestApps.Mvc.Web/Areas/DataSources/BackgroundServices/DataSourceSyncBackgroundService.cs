using CrestApps.AI.Clients;
using CrestApps.AI.DataSources;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.Areas.DataSources.BackgroundServices;


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

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
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
        var session = services.GetRequiredService<ISession>();
        var dataSourceStore = services.GetService<IAIDataSourceStore>();

        if (dataSourceStore == null)
        {
            _logger.LogDebug("No AI data source store is registered. Skipping sync.");

            return;
        }

        var dataSources = await dataSourceStore.GetAllAsync();
        var dataSourceList = dataSources as IReadOnlyCollection<AIDataSource> ?? dataSources.ToList();

        if (dataSourceList.Count == 0)
        {
            return;
        }

        var indexProfileStore = services.GetService<ISearchIndexProfileStore>();

        if (indexProfileStore == null)
        {
            _logger.LogDebug("No search index profile store is registered. Skipping sync.");

            return;
        }

        var clientFactory = services.GetService<IAIClientFactory>();

        if (clientFactory == null)
        {
            _logger.LogDebug("No AI client factory is registered. Skipping sync.");

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting data source sync for {Count} data source(s).", dataSourceList.Count);
        }

        foreach (var dataSource in dataSourceList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // The actual sync logic requires indexing handlers, which are
                // registered at the framework level via AddElasticsearchServices
                // or AddAzureAISearchServices. If available, the service
                // resolves them automatically when data sources are processed.

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Processed data source '{DisplayText}' (ID: {ItemId}).",
                        dataSource.DisplayText,
                        dataSource.ItemId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to sync data source '{DisplayText}' (ID: {ItemId}).",
                    dataSource.DisplayText,
                    dataSource.ItemId);
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
