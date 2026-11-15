using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Who may read an article, by its audience: everyone, anyone signed in, or staff.
/// </summary>
public sealed class KnowledgeArticleAccess
{
    private readonly IAuthorizationService _authorizationService;

    public KnowledgeArticleAccess(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Gets the audiences a user may read.
    /// </summary>
    /// <param name="user">The user, or <see langword="null"/> for a visitor.</param>
    /// <returns>The audiences.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleAudience>> GetAudiencesAsync(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return [KnowledgeArticleAudience.Public];
        }

        return await _authorizationService.AuthorizeAsync(user, KnowledgeBasePermissions.ViewStaffArticles)
            ? [KnowledgeArticleAudience.Public, KnowledgeArticleAudience.SignedIn, KnowledgeArticleAudience.Staff]
            : [KnowledgeArticleAudience.Public, KnowledgeArticleAudience.SignedIn];
    }
}
