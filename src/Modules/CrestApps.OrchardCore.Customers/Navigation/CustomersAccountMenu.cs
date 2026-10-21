using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Adds the customer's addresses and details to the storefront account.
/// </summary>
public sealed class CustomersAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public CustomersAccountMenu(IStringLocalizer<CustomersAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Addresses"], "50", addresses => addresses
                .Id("account-addresses")
                .AddClass("fa-solid fa-location-dot")
                .Action("Addresses", "AccountProfile", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile))
            .Add(S["Account details"], "90", details => details
                .Id("account-details")
                .AddClass("fa-solid fa-id-card")
                .Action("Details", "AccountProfile", new { area = "CrestApps.OrchardCore.Customers" })
                .Permission(CustomerPermissions.ManageOwnCustomerProfile));

        return ValueTask.CompletedTask;
    }
}
