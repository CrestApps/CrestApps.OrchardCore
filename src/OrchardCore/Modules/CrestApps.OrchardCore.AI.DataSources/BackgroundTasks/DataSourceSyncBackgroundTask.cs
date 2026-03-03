using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.DataSources.BackgroundTasks;

[BackgroundTask(
    Title = "AI Data Source Sync",
    Schedule = "*/5 * * * *",
    Description = "Periodically synchronizes data source documents with the master embedding index.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class DataSourceSyncBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var indexingService = serviceProvider.GetRequiredService<DataSourceIndexingService>();

        await indexingService.SyncAllAsync(cancellationToken);
    }
}
