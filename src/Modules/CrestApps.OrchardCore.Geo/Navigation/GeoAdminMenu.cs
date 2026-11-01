using CrestApps.OrchardCore.Geo.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Geo.Navigation;

/// <summary>
/// Adds the territories and the map settings to the admin menu.
/// </summary>
public sealed class GeoAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GeoAdminMenu(IStringLocalizer<GeoAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Territories"], S["Territories"].PrefixPosition("7"), territories => territories
                .Id("geoTerritories")
                .AddClass("geo-territories")
                .Action("Index", "Territories", GeoConstants.Features.Geo)
                .Permission(GeoPermissions.ManageTerritories)
                .LocalNav())
            .Add(S["Settings"], settings => settings
                .Add(S["Maps and Geocoding"], S["Maps and Geocoding"].PrefixPosition(), maps => maps
                    .Id("geoSettings")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", GeoSettingsDisplayDriver.GroupId } })
                    .Permission(GeoPermissions.ManageGeoSettings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
