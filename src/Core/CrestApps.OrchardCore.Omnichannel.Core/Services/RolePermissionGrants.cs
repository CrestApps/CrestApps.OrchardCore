using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Grants permissions to roles that already exist. Orchard Core applies a permission provider's default stereotypes
/// only when its feature is first enabled or a role of that name is created, so a stereotype that gains a permission
/// later never reaches the tenants already running it. A migration calls this to bring those roles in line.
/// </summary>
public static class RolePermissionGrants
{
    /// <summary>
    /// Grants the permissions to the role with the given name, when the tenant has one.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope to work in.</param>
    /// <param name="roleName">The name of the role to grant the permissions to.</param>
    /// <param name="permissions">The permissions to grant.</param>
    public static async Task GrantToRoleAsync(IServiceProvider serviceProvider, string roleName, IEnumerable<Permission> permissions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrEmpty(roleName);

        var roleManager = serviceProvider.GetService<RoleManager<IRole>>();

        if (roleManager is null)
        {
            return;
        }

        var role = await roleManager.FindByNameAsync(roleName);

        if (role is not null)
        {
            await GrantAsync(serviceProvider, roleManager, role, permissions);
        }
    }

    /// <summary>
    /// Grants the permissions to every role that holds the given permission, so a permission split out of an existing
    /// one keeps granting what the roles holding the original had.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope to work in.</param>
    /// <param name="heldPermission">The permission a role must hold to be granted the others.</param>
    /// <param name="permissions">The permissions to grant.</param>
    public static async Task GrantToRolesHoldingAsync(IServiceProvider serviceProvider, Permission heldPermission, IEnumerable<Permission> permissions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(heldPermission);

        var roleService = serviceProvider.GetService<IRoleService>();
        var roleManager = serviceProvider.GetService<RoleManager<IRole>>();

        if (roleService is null || roleManager is null)
        {
            return;
        }

        foreach (var role in await roleService.GetRolesAsync())
        {
            var claims = await roleManager.GetClaimsAsync(role);

            if (claims.Any(claim => claim.Type == Permission.ClaimType && claim.Value == heldPermission.Name))
            {
                await GrantAsync(serviceProvider, roleManager, role, permissions);
            }
        }
    }

    private static async Task GrantAsync(IServiceProvider serviceProvider, RoleManager<IRole> roleManager, IRole role, IEnumerable<Permission> permissions)
    {
        var granted = (await roleManager.GetClaimsAsync(role))
            .Where(claim => claim.Type == Permission.ClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(RolePermissionGrants));

        foreach (var permission in permissions ?? [])
        {
            if (permission is null || !granted.Add(permission.Name))
            {
                continue;
            }

            var result = await roleManager.AddClaimAsync(role, new Claim(Permission.ClaimType, permission.Name));

            if (!result.Succeeded)
            {
                logger?.LogWarning(
                    "The role {Role} could not be granted the permission {Permission}: {Errors}",
                    role.RoleName,
                    permission.Name,
                    string.Join(", ", result.Errors.Select(error => error.Description)));

                continue;
            }

            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                logger.LogInformation("Granted the role {Role} the permission {Permission}.", role.RoleName, permission.Name);
            }
        }
    }
}
