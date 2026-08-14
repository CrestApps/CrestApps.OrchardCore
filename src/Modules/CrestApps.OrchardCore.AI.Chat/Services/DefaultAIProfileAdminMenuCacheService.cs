using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Chat.Services;

using AIChatProfileSettings = Models.AIChatProfileSettings;

internal sealed class DefaultAIProfileAdminMenuCacheService : IAIProfileAdminMenuCacheService
{
    private const string _cacheKey = "AIChat_AdminMenuProfiles";

    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIProfileAdminMenuCacheService"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="serviceScopeFactory">The service scope factory.</param>
    public DefaultAIProfileAdminMenuCacheService(
        IMemoryCache memoryCache,
        IServiceScopeFactory serviceScopeFactory)
    {
        _memoryCache = memoryCache;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Gets the cached AI chat profiles configured for admin-menu display.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async ValueTask<IReadOnlyList<AIProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(_cacheKey, out AIProfile[] cachedProfiles) &&
            cachedProfiles is not null)
        {
            return CloneProfiles(cachedProfiles);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();
        var profiles = await profileStore.GetByTypeAsync(AIProfileType.Chat, cancellationToken);

        var menuProfiles = profiles
            .Where(static profile => profile.TryGetSettings<AIChatProfileSettings>(out var settings) && settings.IsOnAdminMenu)
            .Select(static profile => profile.Clone())
            .ToArray();

        _memoryCache.Set(_cacheKey, menuProfiles, _cacheDuration);

        return CloneProfiles(menuProfiles);
    }

    /// <summary>
    /// Invalidates the cached admin-menu AI profiles.
    /// </summary>
    public ValueTask InvalidateAsync(CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(_cacheKey);

        return ValueTask.CompletedTask;
    }

    private static AIProfile[] CloneProfiles(IEnumerable<AIProfile> profiles) =>
        [.. profiles.Select(static profile => profile.Clone())];
}
