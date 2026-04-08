using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.BackgroundServices;


/// <summary>
/// Daily alignment task that ensures AI data source indexes are fully consistent.
/// Upserts missing documents from data sources and removes orphaned records.
/// Runs once daily at 2:00 AM UTC.
/// </summary>
public sealed class DataSourceAlignmentBackgroundService : BackgroundService
{
    private static readonly TimeSpan _alignmentCheckInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DataSourceAlignmentBackgroundService> _logger;

    public DataSourceAlignmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<DataSourceAlignmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_alignmentCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!ShouldRunAlignment())
            {
                continue;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await AlignDataSourcesAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during daily AI data source alignment.");
            }
        }
    }
    /// <summary>
    /// Determines whether the alignment should run based on the current UTC hour.
    /// Alignment runs daily between 2:00 AM and 2:30 AM UTC.
    /// </summary>
    private bool ShouldRunAlignment()
    {
        var utcNow = _timeProvider.GetUtcNow();

        return utcNow.Hour == 2 && utcNow.Minute < 30;
    }
    /// <summary>
    /// Performs full alignment of all data source indexes by upserting missing
    /// documents and removing orphaned records.
    /// </summary>
    private async Task AlignDataSourcesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dataSourceStore = services.GetService<IAIDataSourceStore>();
        var indexingService = services.GetService<IAIDataSourceIndexingService>();

        if (dataSourceStore == null || indexingService == null)
        {
            _logger.LogDebug("Data source alignment is not fully configured. Skipping alignment.");

            return;
        }

        var dataSources = await dataSourceStore.GetAllAsync();
        var dataSourceList = dataSources?.ToList() ?? [];

        if (dataSourceList.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Starting daily data source alignment for {Count} data source(s).", dataSourceList.Count);
        }

        await indexingService.SyncAllAsync(cancellationToken);

        _logger.LogInformation("Daily data source alignment completed.");
    }
}
