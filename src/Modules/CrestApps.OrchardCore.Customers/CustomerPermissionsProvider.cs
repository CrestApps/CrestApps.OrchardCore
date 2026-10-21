using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers;

internal sealed class CustomerPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CustomerPermissions.ManageCustomers,
        CustomerPermissions.ViewCustomers,
        CustomerPermissions.ManageOwnCustomerProfile,
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
                Permissions = [CustomerPermissions.ManageOwnCustomerProfile],
            },
        ];
    }
}
