namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// The constants of the Geo module.
/// </summary>
public static class GeoConstants
{
    /// <summary>
    /// The YesSql collection Geo records are stored in.
    /// </summary>
    public const string CollectionName = "Geo";

    /// <summary>
    /// The site settings group of the map, geocoding and travel-time settings; provider modules add their sections to
    /// it.
    /// </summary>
    public const string SettingsGroupId = "geo";

    /// <summary>
    /// The name of the built-in travel-time and route provider.
    /// </summary>
    public const string HaversineProviderName = "Haversine";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Geo: points, geocoding, travel times, territories and maps.
        /// </summary>
        public const string Geo = "CrestApps.OrchardCore.Geo";

        /// <summary>
        /// Azure Maps geocoding, travel times and routes.
        /// </summary>
        public const string AzureMaps = "CrestApps.OrchardCore.Geo.AzureMaps";

        /// <summary>
        /// Google Maps geocoding, travel times and routes.
        /// </summary>
        public const string GoogleMaps = "CrestApps.OrchardCore.Geo.GoogleMaps";

        /// <summary>
        /// Travel times and routes from a self-hosted OSRM server.
        /// </summary>
        public const string Osrm = "CrestApps.OrchardCore.Geo.Osrm";
    }
}
