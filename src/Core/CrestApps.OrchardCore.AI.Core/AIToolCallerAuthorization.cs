using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Checks that the person driving a conversation may do what a tool is about to do. A tool runs on behalf of whoever is
/// chatting, a public visitor included, and the model can be steered by what it reads, so a tool that reads private data
/// or changes the site checks the caller's own permission before it acts. No signed-in user means no: a tool run with no
/// request, such as from a background job or post-session processing, is refused too.
/// </summary>
public static class AIToolCallerAuthorization
{
    /// <summary>
    /// The message a tool returns to the model when the caller may not use it.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// Whether the signed-in caller holds the permission.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the tool needs.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission.</returns>
    public static Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission)
        => IsAuthorizedAsync(services, permission, null);

    /// <summary>
    /// Whether the signed-in caller holds the permission over a resource, such as a content item.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the tool needs.</param>
    /// <param name="resource">The resource the tool acts on, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission.</returns>
    public static async Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission, object resource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(permission);

        // The chat hubs publish the connection's caller through the user accessor, where HttpContext may be missing.
        var user = services.GetService<IUserAccessor>()?.User ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission, resource);
    }

    /// <summary>
    /// Whether the caller may use a tool: they hold the permission registered for it
    /// (<see cref="AIToolPermissionOptions"/>), or the tool is marked safe for anyone
    /// (<see cref="AnonymousAIToolOptions"/>). A tool with neither is refused.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="toolName">The name the tool is registered under.</param>
    /// <param name="resource">The resource the tool acts on, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the caller may use the tool.</returns>
    public static Task<bool> IsAuthorizedForToolAsync(IServiceProvider services, string toolName, object resource = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var permission = services.GetService<IOptions<AIToolPermissionOptions>>()?.Value.GetPermission(toolName);

        if (permission is null)
        {
            return Task.FromResult(services.GetService<IOptions<AnonymousAIToolOptions>>()?.Value.IsSafe(toolName) == true);
        }

        return IsAuthorizedAsync(services, permission, resource);
    }
}
