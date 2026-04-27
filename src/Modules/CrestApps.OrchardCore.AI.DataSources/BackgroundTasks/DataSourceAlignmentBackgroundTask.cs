using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.DataSources.BackgroundTasks;

[BackgroundTask(
    Title = "AI Data Source Alignment",
    Schedule = "0 2 * * *",
    Description = "Daily alignment of AI knowledge-base indexes with their mapped data sources.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]

/// <summary>
/// Represents the data source alignment background task.
/// </summary>
public sealed class DataSourceAlignmentBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DataSourceAlignmentBackgroundTask>>();
        var dataSourceStore = serviceProvider.GetService<IAIDataSourceStore>();

        if (dataSourceStore is null)
        {
            return;
        }

        var dataSources = (await dataSourceStore.GetAllAsync(cancellationToken)).ToArray();

        if (dataSources.Length == 0)
        {
            return;
        }

        try
        {
            var indexingService = serviceProvider.GetRequiredService<IAIDataSourceIndexingService>();

            await indexingService.SyncAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during AI data-source alignment background task.");
        }
    }
}
