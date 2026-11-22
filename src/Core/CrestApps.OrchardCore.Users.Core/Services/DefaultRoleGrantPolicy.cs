using OrchardCore.Roles;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// The default <see cref="IRoleGrantPolicy"/>: Orchard Core's system roles (its administrator role by whatever name the
/// tenant gave it) are never granted, and a role is security critical when one of its permissions is, by the flag the
/// permission's provider declares.
/// </summary>
internal sealed class DefaultRoleGrantPolicy : IRoleGrantPolicy
{
    private readonly ISystemRoleProvider _systemRoleProvider;
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;

    public DefaultRoleGrantPolicy(
        ISystemRoleProvider systemRoleProvider,
        IRoleService roleService,
        IPermissionService permissionService)
    {
        _systemRoleProvider = systemRoleProvider;
        _roleService = roleService;
        _permissionService = permissionService;
    }

    public ValueTask<bool> IsGrantableAsync(string roleName, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(RoleGrantRules.IsGrantable(roleName) && !_systemRoleProvider.IsSystemRole(roleName.Trim()));

    public async ValueTask<bool> IsSecurityCriticalAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        roleName = roleName.Trim();

        if (_systemRoleProvider.IsAdminRole(roleName))
        {
            return true;
        }

        foreach (var claim in await _roleService.GetRoleClaimsAsync(roleName, cancellationToken) ?? [])
        {
            if (claim.Type == Permission.ClaimType && await IsSecurityCriticalPermissionAsync(claim.Value))
            {
                return true;
            }
        }

        return false;
    }

    // A permission made for one role or type (AssignRoleToUsers_Editor, ManageUsers_Staff) is named after the general one,
    // an underscore and the role or type, and isn't always listed by its provider: it is as critical as the general one.
    private async ValueTask<bool> IsSecurityCriticalPermissionAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (await _permissionService.FindByNameAsync(name) is { } permission)
        {
            return permission.IsSecurityCritical;
        }

        var separator = name.IndexOf('_');

        return separator > 0 && await _permissionService.FindByNameAsync(name[..separator]) is { IsSecurityCritical: true };
    }
}
