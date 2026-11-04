using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Dispatch;

internal sealed class DispatchPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        DispatchPermissions.ManageDispatchSettings,
        DispatchPermissions.ManageDispatch,
        DispatchPermissions.ViewDispatch,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
