using CrestApps.Core.AI.WebCrawlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.BackgroundTasks;

/// <summary>
/// Hourly Orchard background task that re-crawls every enabled web crawler that is due per its
/// configured re-index interval, indexing only the pages that were added, changed, or removed.
/// </summary>
[BackgroundTask(
    Title = "Web Crawler Re-index",
    Schedule = "0 * * * *",
    Description = "Hourly evaluation of web crawlers; re-crawls and re-indexes each crawler that is due.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class WebCrawlerReindexBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Evaluates the configured web crawlers and re-indexes the ones that are due.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reindexService = serviceProvider.GetService<IWebCrawlerReindexService>();

        if (reindexService is null)
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<WebCrawlerReindexBackgroundTask>>();

        try
        {
            await reindexService.ReindexDueAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while re-indexing due web crawlers.");
        }
    }
}
