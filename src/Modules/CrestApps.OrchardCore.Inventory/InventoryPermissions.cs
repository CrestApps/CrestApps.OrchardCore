using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Inventory;

public static class InventoryPermissions
{
    public static readonly Permission ManageInventory = new(
        "ManageInventory",
        LocalizationSource.Create("Manage inventory", typeof(InventoryPermissions)));

    public static readonly Permission ViewInventory = new(
        "ViewInventory",
        LocalizationSource.Create("View inventory", typeof(InventoryPermissions)),
        [ManageInventory]);
}
