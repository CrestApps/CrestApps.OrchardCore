using CrestApps.OrchardCore.Inventory.Controllers;
using CrestApps.OrchardCore.Inventory.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Inventory.Navigation;

public sealed class PurchaseOrdersAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InventoryConstants.Features.Inventory },
        { "controller", typeof(PurchaseOrdersAdminController).ControllerName() },
        { "action", nameof(PurchaseOrdersAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public PurchaseOrdersAdminMenu(IStringLocalizer<PurchaseOrdersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Purchase orders"], S["Purchase orders"].PrefixPosition("0.31"), orders => orders
                    .AddClass("purchase-orders")
                    .Id("purchaseOrders")
                    .Action(_routeValues)
                    .Permission(InventoryPermissions.ViewInventory)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
