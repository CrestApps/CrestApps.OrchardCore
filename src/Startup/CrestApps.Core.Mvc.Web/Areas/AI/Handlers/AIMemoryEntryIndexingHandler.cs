using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;

namespace CrestApps.Core.Mvc.Web.Areas.AI.Handlers;

public sealed class AIMemoryEntryIndexingHandler : CatalogEntryHandlerBase<AIMemoryEntry>
{
    private readonly AIMemoryIndexingService _indexingService;
    private readonly ILogger<AIMemoryEntryIndexingHandler> _logger;

    public AIMemoryEntryIndexingHandler(
        AIMemoryIndexingService indexingService,
        ILogger<AIMemoryEntryIndexingHandler> logger)
    {
        _indexingService = indexingService;
        _logger = logger;
    }

    public override async Task CreatedAsync(CreatedContext<AIMemoryEntry> context)
    {
        await IndexAsync(context.Model);
    }

    public override async Task UpdatedAsync(UpdatedContext<AIMemoryEntry> context)
    {
        await IndexAsync(context.Model);
    }

    public override async Task DeletedAsync(DeletedContext<AIMemoryEntry> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.ItemId))
        {
            return;
        }

        try
        {
            await _indexingService.DeleteAsync([context.Model.ItemId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove AI memory '{MemoryId}' from the configured memory index.", context.Model.ItemId);
        }
    }

    private async Task IndexAsync(AIMemoryEntry memory)
    {
        try
        {
            await _indexingService.IndexAsync(memory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index AI memory '{MemoryId}' into the configured memory index.", memory.ItemId);
        }
    }
}
