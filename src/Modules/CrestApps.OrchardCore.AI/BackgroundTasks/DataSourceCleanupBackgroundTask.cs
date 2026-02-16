using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.BackgroundTasks;

/// <summary>
/// Daily background task that aligns AI Knowledge Base indexes with their data sources.
/// Upserts missing documents and removes orphaned records belonging to data sources that no longer exist.
/// </summary>
[BackgroundTask(
    Title = "AI Data Source Alignment",
    Schedule = "0 2 * * *",
    Description = "Daily alignment of AI Knowledge Base indexes: upserts missing data and removes orphaned records.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class DataSourceAlignmentBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DataSourceAlignmentBackgroundTask>>();

        try
        {
            var indexingService = serviceProvider.GetRequiredService<DataSourceIndexingService>();

            // Full sync handles both upsert and orphan cleanup.
            await indexingService.SyncAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data source alignment background task.");
        }
    }
}
