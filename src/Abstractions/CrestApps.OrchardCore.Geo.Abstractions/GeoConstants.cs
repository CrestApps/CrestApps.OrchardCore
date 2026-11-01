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
    }
}
