using CrestApps.OrchardCore.AdminTheme.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AdminTheme.Navigation;

/// <summary>
/// Adds the color schemes of the admin under Design, beside Orchard Core's Themes.
/// </summary>
public sealed class AdminThemeAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _routeValues = new()
    {
        { "area", "CrestApps.OrchardCore.AdminTheme" },
        { "controller", typeof(ColorSchemesController).ControllerName() },
        { "action", nameof(ColorSchemesController.Index) },
    };

    internal readonly IStringLocalizer S;

    public AdminThemeAdminMenu(IStringLocalizer<AdminThemeAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Design"], design => design
            .Add(S["Color schemes"], S["Color schemes"].PrefixPosition(), schemes => schemes
                .AddClass("admin-color-schemes")
                .Id("adminColorSchemes")
                .Action(_routeValues)
                .Permission(AdminPermissions.ManageAdminSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
