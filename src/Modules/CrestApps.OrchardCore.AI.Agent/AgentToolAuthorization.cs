using System.Security.Claims;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Checks a permission on one record (a content item, for example) for the signed-in caller of a tool, with the same
/// resource the matching admin action passes, or the permission alone.
/// </summary>
internal static class AgentToolAuthorization
{
    /// <summary>
    /// Gets the signed-in caller of the tool. The chat hubs publish the caller through <see cref="IUserAccessor"/> for the
    /// whole invocation, which also works on transports where the request is not available to the hub method; the
    /// request's user is the fallback.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    public static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Whether the signed-in caller holds the permission.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the tool needs.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission.</returns>
    public static Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission)
        => IsAuthorizedAsync(services, permission, resource: null);

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
