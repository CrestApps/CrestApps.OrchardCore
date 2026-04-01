using CrestApps.OrchardCore.AI.Memory.Services;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly AIMemoryIndexingService _indexingService;

    public AIMemoryIndexProfileHandler(AIMemoryIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (!string.Equals(context.IndexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _indexingService.SyncByIndexProfileIdsAsync([context.IndexProfile.Id]);
    }
}
