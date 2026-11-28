using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// What the user tools share: finding the user a tool acts on, checking the caller's permission on that user the way
/// the Users admin page does, and the guards that page leaves to the person (the caller's own account, the site owner
/// and the last enabled administrator).
/// </summary>
internal static class UserToolAccess
{
    /// <summary>
    /// The largest number of users one call of a tool that takes a list acts on.
    /// </summary>
    public const int MaxUsers = 20;

    /// <summary>
    /// Whether the user is the account of the caller.
    /// </summary>
    /// <param name="caller">The signed-in caller.</param>
    /// <param name="user">The user a tool acts on.</param>
    /// <returns><see langword="true"/> when the caller acts on the caller's own account.</returns>
    public static bool IsCaller(ClaimsPrincipal caller, User user)
        => caller.FindFirstValue(ClaimTypes.NameIdentifier) == user.UserId;

    /// <summary>
    /// Finds the user with the ID and checks the permission of the caller on that user.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="permission">The permission the admin action checks on the user.</param>
    /// <returns>The user, or the sentence the tool returns when there is no such user or the caller may not act on it.</returns>
    public static async Task<(User User, string Error)> FindAsync(IServiceProvider services, string userId, Permission permission)
    {
        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return (null, AIToolCallerAuthorization.NotAllowedMessage);
        }

        var user = string.IsNullOrWhiteSpace(userId)
            ? null
            : await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId.Trim()) as User;

        if (user is null)
        {
            var notFoundMessage = string.IsNullOrWhiteSpace(userId)
                ? "Nothing was done: give the userId of the user. Use listUsers to find it."
                : $"Nothing was done: there is no user with the ID '{userId}'. Use listUsers to find the ID.";

            return (null, await GetNotFoundMessageAsync(services, permission, notFoundMessage));
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, permission, user))
        {
            return (null, AIToolCallerAuthorization.NotAllowedMessage);
        }

        return (user, null);
    }

    /// <summary>
    /// Gets what a tool answers when it did not find the user. A caller who may not act on any user gets the refusal a
    /// user that exists would get, so the tools cannot be used to learn which accounts exist.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the admin action checks on the user.</param>
    /// <param name="notFoundMessage">The sentence for a caller who holds the permission.</param>
    /// <returns>The sentence the tool returns.</returns>
    public static async Task<string> GetNotFoundMessageAsync(IServiceProvider services, Permission permission, string notFoundMessage)
    {
        // The Users admin page checks a permission that has no user yet (the list, the Create page) on a blank user.
        return await AgentToolAuthorization.IsAuthorizedAsync(services, permission, new User())
            ? notFoundMessage
            : AIToolCallerAuthorization.NotAllowedMessage;
    }

    /// <summary>
    /// Whether the user is the site owner (the super user of the site settings), who holds every permission.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns><see langword="true"/> when the user is the site owner.</returns>
    public static async Task<bool> IsSiteOwnerAsync(IServiceProvider services, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var site = await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync();

        return string.Equals(site?.SuperUser, userId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the name of the administrator role when the user holds it. The name is configurable, so it is asked from
    /// the role service.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="roleNames">The names of the roles of the user.</param>
    /// <returns>The name of the administrator role, or <see langword="null"/> when the user is not an administrator.</returns>
    public static async Task<string> GetAdministratorRoleAsync(IServiceProvider services, IEnumerable<string> roleNames)
    {
        var roleService = services.GetRequiredService<IRoleService>();

        foreach (var roleName in roleNames ?? [])
        {
            if (!string.IsNullOrEmpty(roleName) && await roleService.IsAdminRoleAsync(roleName))
            {
                return roleName;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the user is the only enabled user in the administrator role. Disabling or deleting that user, or taking
    /// the role away, would leave the site without an administrator who can sign in.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The user a tool acts on.</param>
    /// <returns><see langword="true"/> when the user is the only enabled administrator.</returns>
    public static async Task<bool> IsOnlyEnabledAdministratorAsync(IServiceProvider services, User user)
    {
        if (!user.IsEnabled)
        {
            return false;
        }

        var administratorRole = await GetAdministratorRoleAsync(services, user.RoleNames);

        if (administratorRole is null)
        {
            return false;
        }

        var administrators = await services.GetRequiredService<UserManager<IUser>>().GetUsersInRoleAsync(administratorRole);

        return !administrators
            .OfType<User>()
            .Any(administrator => administrator.IsEnabled && administrator.UserId != user.UserId);
    }

    /// <summary>
    /// Gets the address of the page of the Users admin area for a user.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="action">The action of the Users admin controller, for example Edit.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminUrl(IServiceProvider services, string action, string userId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        return linkGenerator.GetPathByRouteValues(httpContext, null, new RouteValueDictionary
        {
            { "area", OrchardCoreFeatureIds.Users },
            { "controller", "Admin" },
            { "action", action },
            { "id", userId },
        });
    }

    /// <summary>
    /// Reads the IDs a tool that acts on several users was given.
    /// </summary>
    /// <param name="userIds">The IDs as given.</param>
    /// <returns>The IDs without blanks and repeats, in the given order.</returns>
    public static List<string> CleanIds(IEnumerable<string> userIds)
    {
        var ids = new List<string>();

        foreach (var userId in userIds ?? [])
        {
            var id = userId?.Trim();

            if (!string.IsNullOrEmpty(id) && !ids.Contains(id, StringComparer.Ordinal))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
