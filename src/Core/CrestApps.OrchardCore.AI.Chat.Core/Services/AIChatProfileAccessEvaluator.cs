using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.AI.Chat.Core.Services;

/// <summary>
/// Evaluates whether a caller can access a chat profile.
/// </summary>
public sealed class AIChatProfileAccessEvaluator
{
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatProfileAccessEvaluator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    public AIChatProfileAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Determines whether the current caller can access the requested profile.
    /// </summary>
    /// <param name="user">The current caller.</param>
    /// <param name="profile">The requested profile.</param>
    public Task<bool> CanAccessProfileAsync(ClaimsPrincipal user, AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(profile);

        return _authorizationService.AuthorizeAsync(user, AIPermissions.QueryAnyAIProfile, profile);
    }

    /// <summary>
    /// Returns the profiles the caller can access, in their original order. Use it before a view lists profiles
    /// (template prompts, for example) to a caller, so a profile the caller may not use is never named.
    /// </summary>
    /// <param name="user">The current caller.</param>
    /// <param name="profiles">The candidate profiles.</param>
    public async Task<IReadOnlyList<AIProfile>> GetAccessibleProfilesAsync(ClaimsPrincipal user, IEnumerable<AIProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(user);

        var accessible = new List<AIProfile>();

        foreach (var profile in profiles ?? [])
        {
            if (profile is not null && await CanAccessProfileAsync(user, profile))
            {
                accessible.Add(profile);
            }
        }

        return accessible;
    }
}
