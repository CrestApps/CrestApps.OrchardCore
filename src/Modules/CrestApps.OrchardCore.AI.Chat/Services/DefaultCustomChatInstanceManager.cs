using System.Security.Claims;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class DefaultCustomChatInstanceManager : SourceCatalogManager<AICustomChatInstance>, ICustomChatInstanceManager
{
    private readonly ICustomChatInstanceCatalog _customCatalog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultCustomChatInstanceManager(
        ICustomChatInstanceCatalog catalog,
        IEnumerable<ICatalogEntryHandler<AICustomChatInstance>> handlers,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DefaultCustomChatInstanceManager> logger)
        : base(catalog, handlers, logger)
    {
        _customCatalog = catalog;
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask<IEnumerable<AICustomChatInstance>> GetForCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var instances = await _customCatalog.GetByUserAsync(userId);

        foreach (var instance in instances)
        {
            await LoadAsync(instance);
        }

        return instances;
    }

    public async ValueTask<AICustomChatInstance> FindByIdForCurrentUserAsync(string itemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var instance = await _customCatalog.FindByIdForUserAsync(itemId, userId);

        if (instance != null)
        {
            await LoadAsync(instance);
        }

        return instance;
    }

    private string GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
