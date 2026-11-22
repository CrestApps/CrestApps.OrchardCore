using System.Security.Claims;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Decides who may create or change a local (stdio) MCP connection: only on the default tenant, and only a user who
/// holds <see cref="McpPermissions.ManageMcpStdioConnections"/>. Setup, which runs its recipe while the default tenant is
/// being set up, is the host itself and is allowed. Anyone else without the permission is not: an anonymous caller of a
/// running tenant, and a recipe run with no principal at all (review 11), since Orchard Core gives a background task or a
/// deferred recipe its own context with an anonymous principal or the user who queued it, so no principal is not a sign of
/// the host.
/// </summary>
internal static class McpStdioAccess
{
    /// <summary>
    /// Whether the user may create or change a local (stdio) MCP connection on this tenant.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="user">The user, or <see langword="null"/> when no one is signed in.</param>
    public static async Task<bool> CanManageAsync(ShellSettings shellSettings, IAuthorizationService authorizationService, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(shellSettings);
        ArgumentNullException.ThrowIfNull(authorizationService);

        if (!shellSettings.IsDefaultShell())
        {
            return false;
        }

        if (user?.Identity?.IsAuthenticated != true)
        {
            return shellSettings.IsInitializing();
        }

        return await authorizationService.AuthorizeAsync(user, McpPermissions.ManageMcpStdioConnections);
    }
}
