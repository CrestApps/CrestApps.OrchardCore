using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Gives every sign-in to a parent tenant a random identifier, so signing out ends exactly the delegated access sessions
/// that sign-in started. A refreshed principal keeps the identifier of the sign-in it belongs to.
/// </summary>
public sealed class ParentSessionClaimsProvider : IUserClaimsProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentSessionClaimsProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public ParentSessionClaimsProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        if (claims.HasClaim(claim => claim.Type == TenantHierarchyConstants.ClaimTypes.ParentSessionId))
        {
            return Task.CompletedTask;
        }

        var current = _httpContextAccessor.HttpContext?.User;
        var userId = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var existing = current?.FindFirst(ClaimTypes.NameIdentifier)?.Value == userId
            ? current?.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentSessionId)?.Value
            : null;

        claims.AddClaim(new Claim(
            TenantHierarchyConstants.ClaimTypes.ParentSessionId,
            string.IsNullOrEmpty(existing) ? DelegatedAccessTokens.CreateToken() : existing));

        return Task.CompletedTask;
    }
}
