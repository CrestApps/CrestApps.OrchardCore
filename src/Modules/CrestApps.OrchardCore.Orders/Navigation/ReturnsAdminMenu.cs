using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Navigation;

public sealed class ReturnsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", OrderConstants.Features.Orders },
        { "controller", typeof(ReturnsAdminController).ControllerName() },
        { "action", nameof(ReturnsAdminController.Index) },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", OrderConstants.ReturnsSettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public ReturnsAdminMenu(IStringLocalizer<ReturnsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Returns"], S["Returns"].PrefixPosition("3.5"), returns => returns
                    .AddClass("returns")
                    .Id("returns")
                    .Action(_routeValues)
                    .Permission(OrderPermissions.ManageOrders)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Returns"], S["Returns"].PrefixPosition(), returns => returns
                        .AddClass("returns-settings")
                        .Id("returnsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(OrderPermissions.ManageOrders)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
