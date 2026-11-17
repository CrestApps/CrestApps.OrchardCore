using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

/// <summary>
/// What the role tools share: finding a role the way the Roles admin page does, the permissions of the enabled
/// features with the feature each comes from, and the permissions a role holds.
/// </summary>
internal static class RoleToolSupport
{
    /// <summary>
    /// The largest number of permission names one list of a result holds.
    /// </summary>
    public const int MaxPermissionNames = 200;

    /// <summary>
    /// Finds a role by its name, which is also its ID, as the Roles admin page does.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="roleName">The name of the role, in any casing.</param>
    /// <returns>The role, or <see langword="null"/> when there is none with that name.</returns>
    public static async Task<Role> FindRoleAsync(IServiceProvider services, string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        return await services.GetRequiredService<RoleManager<IRole>>().FindByIdAsync(roleName.Trim()) as Role;
    }

    /// <summary>
    /// Gets the permissions the enabled features provide, the ones the Roles admin page offers, each with the group
    /// that page lists it under: its category, or the feature that provides it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The permissions, one entry for each permission name.</returns>
    public static async Task<List<InstalledPermission>> GetInstalledPermissionsAsync(IServiceProvider services)
    {
        var typeFeatureProvider = services.GetRequiredService<ITypeFeatureProvider>();
        var enabledFeatures = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();
        var enabledFeatureIds = enabledFeatures.Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permissions = new List<InstalledPermission>();

        foreach (var permissionProvider in services.GetServices<IPermissionProvider>())
        {
            // Two features can register the same provider; the admin page names the last one that is enabled.
            var feature = typeFeatureProvider
                .GetFeaturesForDependency(permissionProvider.GetType())
                .LastOrDefault(candidate => enabledFeatureIds.Contains(candidate.Id));

            foreach (var permission in await permissionProvider.GetPermissionsAsync())
            {
                // A permission that two providers return is one permission: a role holds it by its name.
                if (permission is null || !names.Add(permission.Name))
                {
                    continue;
                }

                var group = !string.IsNullOrWhiteSpace(permission.Category)
                    ? permission.Category
                    : !string.IsNullOrWhiteSpace(feature?.Name)
                        ? feature.Name
                        : feature?.Id;

                permissions.Add(new InstalledPermission(permission, group, feature?.Id));
            }
        }

        return permissions;
    }

    /// <summary>
    /// Gets the names of the permissions that are assigned to a role directly.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The permission names, without repeats, in the stored order.</returns>
    public static List<string> GetAssignedPermissionNames(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var names = new List<string>();

        foreach (var claim in role.RoleClaims ?? [])
        {
            if (claim.ClaimType == Permission.ClaimType &&
                !string.IsNullOrEmpty(claim.ClaimValue) &&
                !names.Contains(claim.ClaimValue, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(claim.ClaimValue);
            }
        }

        return names;
    }

    /// <summary>
    /// Gets the address of the page of the Roles admin area for a role.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="roleName">The name of the role.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetAdminUrl(IServiceProvider services, string roleName)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        return linkGenerator.GetPathByRouteValues(httpContext, null, new RouteValueDictionary
        {
            { "area", OrchardCoreFeatureIds.Roles },
            { "controller", "Admin" },
            { "action", "Edit" },
            { "id", roleName },
        });
    }

    /// <summary>
    /// Keeps the names that contain the filter text and caps the list.
    /// </summary>
    /// <param name="names">The permission names.</param>
    /// <param name="filter">The text a name must contain, or <see langword="null"/> for every name.</param>
    /// <param name="truncated">Set to <see langword="true"/> when names were left out because of the cap.</param>
    /// <returns>The names to return, in alphabetical order.</returns>
    public static List<string> Limit(IEnumerable<string> names, string filter, ref bool truncated)
    {
        var matching = names
            .Where(name => string.IsNullOrEmpty(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matching.Count <= MaxPermissionNames)
        {
            return matching;
        }

        truncated = true;

        return matching.Take(MaxPermissionNames).ToList();
    }
}
