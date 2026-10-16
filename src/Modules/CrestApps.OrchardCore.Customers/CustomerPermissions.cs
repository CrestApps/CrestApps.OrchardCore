using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers;

public static class CustomerPermissions
{
    public static readonly Permission ManageCustomers = new("ManageCustomers", "Manage customers");

    public static readonly Permission ViewCustomers = new("ViewCustomers", "View customers", [ManageCustomers]);
}

internal sealed class CustomerPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CustomerPermissions.ManageCustomers,
        CustomerPermissions.ViewCustomers,
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
