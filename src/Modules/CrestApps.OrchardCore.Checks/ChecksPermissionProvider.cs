using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checks;

internal sealed class ChecksPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ChecksPermissions.ManageCheckLayouts,
        ChecksPermissions.ManageChecks,
        ChecksPermissions.ViewCheckRegister,
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
