using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Navigation;

public sealed class OrdersAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _ordersRouteValues = new()
    {
        { "area", OrderConstants.Features.Orders },
        { "controller", typeof(OrdersAdminController).ControllerName() },
        { "action", nameof(OrdersAdminController.Index) },
    };

    private static readonly RouteValueDictionary _myOrdersRouteValues = new()
    {
        { "area", OrderConstants.Features.Orders },
        { "controller", typeof(OrdersAdminController).ControllerName() },
        { "action", nameof(OrdersAdminController.Mine) },
    };

    internal readonly IStringLocalizer S;

    public OrdersAdminMenu(IStringLocalizer<OrdersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Orders"], S["Orders"].PrefixPosition("3"), orders => orders
                    .Id("orders")
                    .Action(_ordersRouteValues)
                    .Permission(OrderPermissions.ManageOrders)
                    .LocalNav()
                )
                .Add(S["My Orders"], S["My Orders"].PrefixPosition("4"), orders => orders
                    .Id("my-orders")
                    .Action(_myOrdersRouteValues)
                    .Permission(OrderPermissions.ViewOwnOrders)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
