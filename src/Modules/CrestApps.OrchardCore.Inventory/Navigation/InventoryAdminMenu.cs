using CrestApps.OrchardCore.Inventory.Controllers;
using CrestApps.OrchardCore.Inventory.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Inventory.Navigation;

public sealed class InventoryAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", InventoryConstants.Features.Inventory },
        { "controller", typeof(InventoryAdminController).ControllerName() },
        { "action", nameof(InventoryAdminController.Index) },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", InventoryConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public InventoryAdminMenu(IStringLocalizer<InventoryAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Inventory"], S["Inventory"].PrefixPosition("0.3"), inventory => inventory
                    .Id("inventory")
                    .Action(_routeValues)
                    .Permission(InventoryPermissions.ViewInventory)
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Inventory"], S["Inventory"].PrefixPosition(), inventory => inventory
                        .Id("inventorySettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(InventoryPermissions.ManageInventory)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
