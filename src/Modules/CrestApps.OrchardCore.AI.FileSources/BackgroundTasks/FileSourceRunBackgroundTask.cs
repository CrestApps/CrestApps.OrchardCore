using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.FileSources;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.FileSources.BackgroundTasks;

/// <summary>
/// Hourly Orchard background task that runs every enabled file source whose interval has elapsed.
/// </summary>
/// <remarks>
/// The framework ships a hosted service that does this, but a hosted service registered in a tenant's
/// container is never started, so the schedule is driven from here instead. The run itself is entirely the
/// framework's <see cref="IFileSourceRunService"/>; this only decides which sources are due.
/// </remarks>
[BackgroundTask(
    Title = "File Source Ingestion",
    Schedule = "0 * * * *",
    Description = "Hourly evaluation of file sources; reads and ingests each source that is due.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class FileSourceRunBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Runs the file sources that are due.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var runService = serviceProvider.GetService<IFileSourceRunService>();
        var store = serviceProvider.GetService<IWebCrawlerStore>();
        var dataSourceStore = serviceProvider.GetService<IAIDataSourceStore>();
        var connectorResolver = serviceProvider.GetService<IIngestionConnectorResolver>();

        if (runService is null || store is null || dataSourceStore is null || connectorResolver is null)
        {
            return;
        }

        var options = serviceProvider.GetRequiredService<IOptions<FileSourceOptions>>().Value;
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        var logger = serviceProvider.GetRequiredService<ILogger<FileSourceRunBackgroundTask>>();
        var now = timeProvider.GetUtcNow();

        foreach (var fileSource in await store.GetAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!fileSource.Enabled || string.IsNullOrWhiteSpace(fileSource.AIDataSourceId))
            {
                continue;
            }

            // A record whose source is not a registered connector is a web crawler, or the leftovers of a
            // feature that is gone. Either way it is not ours to run.
            if (connectorResolver.Get(fileSource.Source) is null)
            {
                continue;
            }

            if (!await FeedsFileDataSourceAsync(dataSourceStore, fileSource, logger, cancellationToken))
            {
                continue;
            }

            if (!IsDue(fileSource, options, now))
            {
                continue;
            }

            try
            {
                var summary = await runService.RunAsync(fileSource, cancellationToken);

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "File source {FileSourceId} saw {Discovered} item(s), ingested {Ingested}, removed {Removed}, failed {Failed}. Listing complete: {Complete}.",
                        fileSource.ItemId,
                        summary.ItemsDiscovered,
                        summary.ItemsIndexed,
                        summary.ItemsDeleted,
                        summary.ItemsFailed,
                        summary.DiscoveryCompleted);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One file source that cannot run is one file source. The others still get their turn.
                logger.LogError(ex, "File source {FileSourceId} failed to run.", fileSource.ItemId);
            }
        }
    }

    /// <summary>
    /// Decides whether a file source is due, from the run summary stored on it.
    /// </summary>
    /// <param name="fileSource">The stored file source.</param>
    /// <param name="options">The file source options.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> when it has never run or its interval has elapsed.</returns>
    private static bool IsDue(WebCrawler fileSource, FileSourceOptions options, DateTimeOffset now)
    {
        var every = TimeSpan.FromMinutes(Math.Max(1, fileSource.ReindexIntervalMinutes ?? options.DefaultRunIntervalMinutes));

        if (!fileSource.TryGet<IndexerRunSummary>(out var last) || last.StartedUtc == default)
        {
            return true;
        }

        return now - new DateTimeOffset(last.StartedUtc, TimeSpan.Zero) >= every;
    }

    /// <summary>
    /// Determines whether the record feeds a File data source, which is what separates a file source from a
    /// web crawler pointed at a Web data source.
    /// </summary>
    /// <param name="dataSourceStore">The data source store.</param>
    /// <param name="fileSource">The stored file source.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the target data source is a File one.</returns>
    private static async Task<bool> FeedsFileDataSourceAsync(
        IAIDataSourceStore dataSourceStore,
        WebCrawler fileSource,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataSource = await dataSourceStore.FindByIdAsync(fileSource.AIDataSourceId, cancellationToken);

            return dataSource is not null &&
                string.Equals(dataSource.Source, AIDataSourceSourceTypes.File, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the data source of file source {FileSourceId}.", fileSource.ItemId);

            return false;
        }
    }
}
