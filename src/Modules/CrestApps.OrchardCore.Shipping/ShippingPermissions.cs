using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Shipping;

public static class ShippingPermissions
{
    public static readonly Permission ManageShipping = new("ManageShipping", "Manage shipping zones, methods and settings");

    public static readonly Permission FulfillOrders = new("FulfillOrders", "Ship orders and update their shipments");
}

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
    ];
}
