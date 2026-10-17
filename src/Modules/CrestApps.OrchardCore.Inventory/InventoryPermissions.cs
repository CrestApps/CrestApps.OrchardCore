using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory;

public static class InventoryPermissions
{
    public static readonly Permission ManageInventory = new("ManageInventory", "Manage inventory");

    public static readonly Permission ViewInventory = new("ViewInventory", "View inventory", [ManageInventory]);
}

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
