using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Orders;

public static class OrderPermissions
{
    public static readonly Permission ManageOrders = new("ManageOrders", "Manage orders");

    public static readonly Permission ViewOwnOrders = new("ViewOwnOrders", "View own orders");

    public static readonly Permission ManageGiftCards = new("ManageGiftCards", "Manage gift cards");
}

internal sealed class GiftCardPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        OrderPermissions.ManageGiftCards,
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

internal sealed class OrderPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        OrderPermissions.ManageOrders,
        OrderPermissions.ViewOwnOrders,
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
                Permissions = [OrderPermissions.ViewOwnOrders],
            },
        ];
    }
}
