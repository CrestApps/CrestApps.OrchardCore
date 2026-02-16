using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.Services;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

/// <summary>
/// Handles index profile synchronization events for data source master indexes.
/// When a master index is synchronized (e.g., created or reset), triggers a full re-sync
/// of all data source documents into the master embedding index.
/// </summary>
public sealed class DataSourceIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly DataSourceIndexingService _indexingService;

    public DataSourceIndexProfileHandler(DataSourceIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (!string.Equals(DataSourceConstants.IndexingTaskType, context.IndexProfile.Type, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _indexingService.SyncByIndexProfileIdsAsync([context.IndexProfile.Id]);
    }
}
