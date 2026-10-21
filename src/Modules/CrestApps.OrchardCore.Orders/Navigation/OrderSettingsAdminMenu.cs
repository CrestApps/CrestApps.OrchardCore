using CrestApps.OrchardCore.Orders.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Navigation;

/// <summary>
/// Settings › Commerce › Orders.
/// </summary>
public sealed class OrderSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", OrderConstants.SettingsGroupId },
    };

    private static readonly RouteValueDictionary _emailRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", Drivers.OrderEmailTemplateSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public OrderSettingsAdminMenu(IStringLocalizer<OrderSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Orders"], S["Orders"].PrefixPosition(), orders => orders
                        .Id("ordersSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(OrderPermissions.ManageOrders)
                        .LocalNav()
                    )
                    .Add(S["Emails"], S["Emails"].PrefixPosition(), emails => emails
                        .Id("orderEmailsSettings")
                        .Action("Index", "Admin", _emailRouteValues)
                        .Permission(OrderPermissions.ManageOrders)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
