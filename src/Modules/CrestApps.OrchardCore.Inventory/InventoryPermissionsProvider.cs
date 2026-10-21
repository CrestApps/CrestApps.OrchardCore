using CrestApps.OrchardCore.Commerce;
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
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions =
                [
                    InventoryPermissions.ManageInventory,
                    InventoryPermissions.ViewInventory,
                ],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Warehouse,
                Permissions =
                [
                    InventoryPermissions.ManageInventory,
                    InventoryPermissions.ViewInventory,
                ],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.CustomerSupport,
                Permissions = [InventoryPermissions.ViewInventory],
            },
        ];
    }
}
