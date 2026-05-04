using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Services;

namespace CrestApps.OrchardCore.AI.Chat.Handlers;

internal sealed class AIProfileAdminMenuCacheHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly IAIProfileAdminMenuCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileAdminMenuCacheHandler"/> class.
    /// </summary>
    /// <param name="cacheService">The cache service.</param>
    public AIProfileAdminMenuCacheHandler(IAIProfileAdminMenuCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public override async Task CreatedAsync(CreatedContext<AIProfile> context, CancellationToken cancellationToken = default)
    {
        await _cacheService.InvalidateAsync();
    }

    public override async Task UpdatedAsync(UpdatedContext<AIProfile> context, CancellationToken cancellationToken = default)
    {
        await _cacheService.InvalidateAsync();
    }

    public override async Task DeletedAsync(DeletedContext<AIProfile> context, CancellationToken cancellationToken = default)
    {
        await _cacheService.InvalidateAsync();
    }
}
