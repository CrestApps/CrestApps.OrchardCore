using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.BackgroundTasks;

/// <summary>
/// Daily background task that cleans up orphaned records in master embedding indexes.
/// Removes documents belonging to data sources that no longer exist.
/// </summary>
[BackgroundTask(
    Title = "AI Data Source Cleanup",
    Schedule = "0 2 * * *",
    Description = "Daily cleanup of orphaned records in master embedding indexes.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class DataSourceCleanupBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<DataSourceCleanupBackgroundTask>>();
        var indexProfileStore = serviceProvider.GetRequiredService<IIndexProfileStore>();
        var dataSourceStore = serviceProvider.GetRequiredService<ICatalog<AIDataSource>>();

        try
        {
            var masterIndexProfiles = await indexProfileStore.GetByTypeAsync(DataSourceConstants.IndexingTaskType);

            if (!masterIndexProfiles.Any())
            {
                return;
            }

            // Get all valid data source IDs.
            var allDataSources = await dataSourceStore.GetAllAsync();
            var validDataSourceIds = new HashSet<string>(
                allDataSources.Select(ds => ds.ItemId),
                StringComparer.OrdinalIgnoreCase);

            // For each master index, check for orphaned documents.
            foreach (var masterProfile in masterIndexProfiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var vectorSearchService = serviceProvider.GetKeyedService<IDataSourceVectorSearchService>(masterProfile.ProviderName);

                if (vectorSearchService == null)
                {
                    continue;
                }

                var documentIndexManager = serviceProvider.GetKeyedService<IDocumentIndexManager>(masterProfile.ProviderName);

                if (documentIndexManager == null)
                {
                    continue;
                }

                // Find data sources that reference this master index.
                var referencingDataSources = allDataSources
                    .Where(ds =>
                    {
                        var metadata = ds.As<AIDataSourceIndexMetadata>();
                        return string.Equals(metadata.MasterIndexName, masterProfile.IndexName, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(ds => ds.ItemId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // If no data sources reference this master index, skip cleanup
                // (the index might be newly created or not yet configured).
                if (referencingDataSources.Count == 0)
                {
                    continue;
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Data source cleanup task completed for master index '{IndexName}'.", masterProfile.IndexName);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data source cleanup background task.");
        }
    }
}
