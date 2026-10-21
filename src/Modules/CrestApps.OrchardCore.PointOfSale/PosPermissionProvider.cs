using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PointOfSale;

internal sealed class PosPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        PosPermissions.SellAtRegister,
        PosPermissions.ManageRegisters,
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
