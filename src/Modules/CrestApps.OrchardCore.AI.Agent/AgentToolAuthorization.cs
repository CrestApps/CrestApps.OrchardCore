using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Checks a permission on one record (a content item, for example) for the signed-in caller of a tool, with the same
/// resource the matching admin action passes. A check that has no resource uses
/// <c>AIToolCallerAuthorization.IsAuthorizedAsync</c> from <c>CrestApps.OrchardCore.AI.Core</c>.
/// </summary>
internal static class AgentToolAuthorization
{
    /// <summary>
    /// Gets the signed-in caller of the tool.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    public static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Whether the signed-in caller holds the permission on the resource.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the tool needs.</param>
    /// <param name="resource">The record the permission is checked on.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission on the resource.</returns>
    public static async Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission, object resource)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var user = GetCaller(services);

        if (user is null)
        {
            return false;
        }

        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission, resource);
    }
}
