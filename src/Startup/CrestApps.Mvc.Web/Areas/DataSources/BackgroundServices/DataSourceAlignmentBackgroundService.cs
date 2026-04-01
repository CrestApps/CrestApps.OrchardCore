using CrestApps.AI.DataSources;
using CrestApps.AI.Models;
using ISession = YesSql.ISession;

namespace CrestApps.Mvc.Web.BackgroundTasks;

/// <summary>
/// Daily alignment task that ensures AI data source indexes are fully consistent.
/// Upserts missing documents from data sources and removes orphaned records.
/// Runs once daily at 2:00 AM UTC.
/// </summary>
public sealed class DataSourceAlignmentBackgroundService : BackgroundService
{
    private static readonly TimeSpan _alignmentCheckInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataSourceAlignmentBackgroundService> _logger;

    public DataSourceAlignmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataSourceAlignmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_alignmentCheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
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
    private static bool ShouldRunAlignment()
    {
        var utcNow = DateTime.UtcNow;

        return utcNow.Hour == 2 && utcNow.Minute < 30;
    }
    /// <summary>
    /// Performs full alignment of all data source indexes by upserting missing
    /// documents and removing orphaned records.
    /// </summary>
    private async Task AlignDataSourcesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dataSourceStore = services.GetService<IAIDataSourceStore>();

        if (dataSourceStore == null)
        {
            _logger.LogDebug("No AI data source store is registered. Skipping alignment.");

            return;
        }

        var dataSources = await dataSourceStore.GetAllAsync();
        var dataSourceList = dataSources as IReadOnlyCollection<AIDataSource> ?? dataSources.ToList();

        if (dataSourceList.Count == 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Starting daily data source alignment for {Count} data source(s).", dataSourceList.Count);
        }

        var session = services.GetRequiredService<ISession>();

        foreach (var dataSource in dataSourceList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Aligned data source '{DisplayText}' (ID: {ItemId}).",
                        dataSource.DisplayText,
                        dataSource.ItemId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to align data source '{DisplayText}' (ID: {ItemId}).",
                    dataSource.DisplayText,
                    dataSource.ItemId);
            }
        }

        await session.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Daily data source alignment completed.");
    }
}
