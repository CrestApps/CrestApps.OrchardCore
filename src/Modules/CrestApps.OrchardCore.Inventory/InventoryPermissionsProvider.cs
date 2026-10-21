using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory;

internal sealed class InventoryPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        InventoryPermissions.ManageInventory,
        InventoryPermissions.ViewInventory,
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
        ];
    }
}
