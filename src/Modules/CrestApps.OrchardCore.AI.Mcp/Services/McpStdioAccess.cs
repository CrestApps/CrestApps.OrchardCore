using System.Security.Claims;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Decides who may create or change a local (stdio) MCP connection: only on the default tenant, and only a user who
/// holds <see cref="McpPermissions.ManageMcpStdioConnections"/>. A recipe run by the host itself is allowed on the default
/// tenant: a startup recipe, which runs with no principal at all, and setup, which runs under an anonymous principal while
/// the tenant is being set up. An anonymous caller of a running tenant is not.
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
            return user is null || shellSettings.IsInitializing();
        }

        return await authorizationService.AuthorizeAsync(user, McpPermissions.ManageMcpStdioConnections);
    }
}
