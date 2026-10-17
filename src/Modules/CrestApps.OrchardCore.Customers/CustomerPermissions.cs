using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers;

public static class CustomerPermissions
{
    public static readonly Permission ManageCustomers = new("ManageCustomers", "Manage customers");

    public static readonly Permission ViewCustomers = new("ViewCustomers", "View customers", [ManageCustomers]);

    /// <summary>
    /// A signed-in customer's own profile and address book (My Account).
    /// </summary>
    public static readonly Permission ManageOwnCustomerProfile = new("ManageOwnCustomerProfile", "Manage own customer profile and addresses");
}

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
