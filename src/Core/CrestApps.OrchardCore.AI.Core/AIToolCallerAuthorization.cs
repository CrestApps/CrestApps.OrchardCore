using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Checks that the person driving a conversation may do what a tool is about to do. A tool runs on behalf of whoever is
/// chatting, a public visitor included, and the model can be steered by what it reads, so a tool that changes the site
/// (creates tenants, imports recipes) checks the caller's own permission before it acts. No signed-in user means no.
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
    public static async Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(permission);

        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission);
    }
}
