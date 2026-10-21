using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Orders;

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
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = [OrderPermissions.ManageOrders],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.CustomerSupport,
                Permissions = [OrderPermissions.ManageOrders],
            },
        ];
    }
}
