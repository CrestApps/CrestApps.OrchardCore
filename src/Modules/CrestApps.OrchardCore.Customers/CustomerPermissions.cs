using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers;

public static class CustomerPermissions
{
    public static readonly Permission ManageCustomers = new(
        "ManageCustomers",
        LocalizationSource.Create("Manage customers", typeof(CustomerPermissions)));

    public static readonly Permission ViewCustomers = new(
        "ViewCustomers",
        LocalizationSource.Create("View customers", typeof(CustomerPermissions)),
        [ManageCustomers]);

    /// <summary>
    /// A signed-in customer's own profile and address book (My Account).
    /// </summary>
    public static readonly Permission ManageOwnCustomerProfile = new(
        "ManageOwnCustomerProfile",
        LocalizationSource.Create("Manage own customer profile and addresses", typeof(CustomerPermissions)));
}
