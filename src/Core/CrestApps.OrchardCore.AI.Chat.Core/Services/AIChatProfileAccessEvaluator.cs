using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Evaluates whether a caller can access a chat profile.
/// </summary>
public sealed class AIChatProfileAccessEvaluator
{
    private const string _publicWidgetProfileIdsCacheKey = "CrestApps.OrchardCore.AI.Chat.PublicWidgetProfileIds";

    private readonly IAuthorizationService _authorizationService;
    private readonly IMemoryCache _memoryCache;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatProfileAccessEvaluator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="session">The YesSql session.</param>
    public AIChatProfileAccessEvaluator(
        IAuthorizationService authorizationService,
        IMemoryCache memoryCache,
        ISession session)
    {
        _authorizationService = authorizationService;
        _memoryCache = memoryCache;
        _session = session;
    }

    /// <summary>
    /// Determines whether the current caller can access the requested profile.
    /// </summary>
    /// <param name="user">The current caller.</param>
    /// <param name="profile">The requested profile.</param>
    public async Task<bool> CanAccessProfileAsync(ClaimsPrincipal user, AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(profile);

        if (user.Identity?.IsAuthenticated == true)
        {
            return await _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile, profile);
        }

        if (profile.Type != AIProfileType.Chat)
        {
            return false;
        }

        var publicProfileIds = await GetPublicWidgetProfileIdsAsync();

        return publicProfileIds.Contains(profile.ItemId);
    }

    private async Task<HashSet<string>> GetPublicWidgetProfileIdsAsync()
    {
        return await _memoryCache.GetOrCreateAsync(_publicWidgetProfileIdsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);

            var contentItems = await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == "AIChat" &&
                index.Published)
                .ListAsync();

            return contentItems
                .Select(contentItem =>
                {
                    return contentItem.TryGet<AIProfilePart>(out var part)
                        ? part.ProfileId
                        : null;
                })
                .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
                .Select(profileId => profileId!)
                .ToHashSet(StringComparer.Ordinal);
        });
    }
}
