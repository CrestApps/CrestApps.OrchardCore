using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

internal sealed class PortalPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.UseWorkforcePortal,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = _allPermissions,
            },
        ];
    }
}
