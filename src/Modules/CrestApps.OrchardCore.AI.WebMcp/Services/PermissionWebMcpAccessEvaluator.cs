using System.Security.Claims;
using CrestApps.Core.AI.WebMcp.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Lets a user use the WebMCP tool endpoints only when they hold the
/// <see cref="WebMcpPermissionsProvider.UseWebMcpTools"/> permission. Each tool also needs the user's permission for
/// that tool, which CrestApps.Core checks with <c>IAIToolAccessEvaluator</c>.
/// </summary>
internal sealed class PermissionWebMcpAccessEvaluator : IWebMcpAccessEvaluator
{
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionWebMcpAccessEvaluator"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    public PermissionWebMcpAccessEvaluator(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Determines whether the user may use WebMCP tools.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the user is signed in and holds the permission.</returns>
    public async Task<bool> CanUseAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.UseWebMcpTools);
    }
}
