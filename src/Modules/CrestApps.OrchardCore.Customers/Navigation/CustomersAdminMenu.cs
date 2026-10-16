using CrestApps.OrchardCore.Customers.Controllers;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Customers.Navigation;

public sealed class CustomersAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", CustomersConstants.Features.Customers },
        { "controller", typeof(CustomersAdminController).ControllerName() },
        { "action", nameof(CustomersAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public CustomersAdminMenu(IStringLocalizer<CustomersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .AddClass("commerce")
                .Id("commerce")
                .Add(S["Customers"], S["Customers"].PrefixPosition("2"), customers => customers
                    .AddClass("customers")
                    .Id("customers")
                    .Action(_routeValues)
                    .Permission(CustomerPermissions.ViewCustomers)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
