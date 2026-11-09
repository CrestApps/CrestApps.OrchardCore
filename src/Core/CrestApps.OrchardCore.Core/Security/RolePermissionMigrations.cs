using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// Carries the roles of a running tenant over when a permission is replaced, renamed or split. Orchard Core applies a
/// role's default permissions only when a feature is first enabled or the role is created, so without this a role that
/// could open a screen yesterday would be locked out of it today. Call it from a deferred task in a data migration.
/// </summary>
public static class RolePermissionMigrations
{
    /// <summary>
    /// Grants every role that holds one of the keys the permissions listed against it.
    /// </summary>
    /// <param name="serviceProvider">The services of the scope to work in.</param>
    /// <param name="grantsByHeldPermission">The permissions to grant, by the permission a role must already hold.</param>
    /// <param name="logger">The logger.</param>
    public static async Task GrantAsync(
        IServiceProvider serviceProvider,
        IReadOnlyDictionary<string, string[]> grantsByHeldPermission,
        ILogger logger)
    {
        var roleService = serviceProvider.GetService<IRoleService>();
        var roleManager = serviceProvider.GetService<RoleManager<IRole>>();

        if (roleService is null || roleManager is null)
        {
            return;
        }

        foreach (var role in await roleService.GetRolesAsync())
        {
            if (role is not Role editable || editable.RoleClaims is null)
            {
                continue;
            }

            var granted = editable.RoleClaims
                .Where(claim => claim.ClaimType == Permission.ClaimType)
                .Select(claim => claim.ClaimValue)
                .ToHashSet(StringComparer.Ordinal);

            var additions = grantsByHeldPermission
                .Where(grant => granted.Contains(grant.Key))
                .SelectMany(grant => grant.Value)
                .Where(permission => !granted.Contains(permission))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (additions.Length == 0)
            {
                continue;
            }

            foreach (var permission in additions)
            {
                editable.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = permission });
            }

            await roleManager.UpdateAsync(editable);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Granted the role {Role} the permissions {Permissions}.", editable.RoleName, string.Join(", ", additions));
            }
        }
    }
}
