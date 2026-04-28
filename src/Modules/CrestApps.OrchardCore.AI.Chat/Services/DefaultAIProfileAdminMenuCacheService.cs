using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace CrestApps.OrchardCore.AI.Chat.Services;

using AIChatProfileSettings = Models.AIChatProfileSettings;

internal sealed class DefaultAIProfileAdminMenuCacheService : IAIProfileAdminMenuCacheService
{
    private const string _cacheKey = "AIChat_AdminMenuProfiles";

    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _memoryCache;
    private readonly IAIProfileStore _aIProfileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIProfileAdminMenuCacheService"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="serviceScopeFactory">The service scope factory.</param>
    public DefaultAIProfileAdminMenuCacheService(
        IMemoryCache memoryCache,
        IAIProfileStore aIProfileStore)
    {
        _memoryCache = memoryCache;
        _aIProfileStore = aIProfileStore;
    }

    /// <summary>
    /// Gets the cached AI chat profiles configured for admin-menu display.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async ValueTask<IReadOnlyList<AIProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(_cacheKey, out AIProfile[] cachedProfiles))
        {
            return cachedProfiles;
        }

        var profiles = await _aIProfileStore.GetByTypeAsync(AIProfileType.Chat, cancellationToken);

        var menuProfiles = profiles
            .Where(static profile => profile.TryGetSettings<AIChatProfileSettings>(out var settings) && settings.IsOnAdminMenu)
            .Select(static profile => profile.Clone())
            .ToArray();

        _memoryCache.Set(_cacheKey, menuProfiles, _cacheDuration);

        return menuProfiles;
    }

    /// <summary>
    /// Invalidates the cached admin-menu AI profiles.
    /// </summary>
    public ValueTask InvalidateAsync()
    {
        _memoryCache.Remove(_cacheKey);

        return ValueTask.CompletedTask;
    }
}
