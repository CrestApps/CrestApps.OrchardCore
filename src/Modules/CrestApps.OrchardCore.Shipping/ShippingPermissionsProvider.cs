using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Shipping;

internal sealed class ShippingPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ShippingPermissions.ManageShipping,
        ShippingPermissions.FulfillOrders,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
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
                ShippingPermissions.ManageShipping,
                ShippingPermissions.FulfillOrders,
            ],
        },
        new PermissionStereotype
        {
            Name = CommerceConstants.StaffRoles.Warehouse,
            Permissions = [ShippingPermissions.FulfillOrders],
        },
    ];
}
