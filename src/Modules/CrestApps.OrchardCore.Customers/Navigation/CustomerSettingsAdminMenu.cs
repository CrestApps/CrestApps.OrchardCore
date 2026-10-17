using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

/// <summary>
/// Settings › Commerce › Customers, with what an order's checkout asks the buyer.
/// </summary>
public sealed class CustomerSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", CustomersConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public CustomerSettingsAdminMenu(IStringLocalizer<CustomerSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Customers"], S["Customers"].PrefixPosition(), customers => customers
                        .AddClass("customers-settings")
                        .Id("customersSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(CustomerPermissions.ManageCustomers)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
