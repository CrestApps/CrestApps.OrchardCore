using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// The permissions of the Geo module.
/// </summary>
public static class GeoPermissions
{
    /// <summary>
    /// Allows managing service territories.
    /// </summary>
    public static readonly Permission ManageTerritories = new("ManageGeoTerritories", LocalizationSource.Create("Manage service territories", typeof(GeoPermissions)));

    /// <summary>
    /// Allows managing the map, geocoding and travel-time settings.
    /// </summary>
    public static readonly Permission ManageGeoSettings = new("ManageGeoSettings", LocalizationSource.Create("Manage maps and geocoding settings", typeof(GeoPermissions)));
}
