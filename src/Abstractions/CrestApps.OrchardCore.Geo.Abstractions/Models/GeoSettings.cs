namespace CrestApps.OrchardCore.Geo.Models;

/// <summary>
/// The site's map, geocoding and travel-time settings.
/// </summary>
public sealed class GeoSettings
{
    /// <summary>
    /// The default map tile address: the OpenStreetMap tile server, for trying things out only.
    /// </summary>
    public const string DefaultTileUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// The default tile attribution.
    /// </summary>
    public const string DefaultTileAttribution = "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OpenStreetMap</a> contributors";

    /// <summary>
    /// Gets or sets the geocoding provider, or <see langword="null"/> for none.
    /// </summary>
    public string GeocodingProvider { get; set; }

    /// <summary>
    /// Gets or sets the travel-time provider, or <see langword="null"/> for the built-in estimate.
    /// </summary>
    public string TravelTimeProvider { get; set; }

    /// <summary>
    /// Gets or sets the route provider, or <see langword="null"/> for straight lines.
    /// </summary>
    public string RouteProvider { get; set; }

    /// <summary>
    /// Gets or sets the average speed of the built-in estimate, in km/h.
    /// </summary>
    public double AverageSpeedKmh { get; set; } = 40;

    /// <summary>
    /// Gets or sets how much longer roads are than a straight line, for the built-in estimate.
    /// </summary>
    public double RoadFactor { get; set; } = 1.3;

    /// <summary>
    /// Gets or sets how long travel times are cached, in minutes.
    /// </summary>
    public int MatrixCacheMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the map tile address, with <c>{z}</c>, <c>{x}</c> and <c>{y}</c>.
    /// </summary>
    public string TileUrl { get; set; } = DefaultTileUrl;

    /// <summary>
    /// Gets or sets the map's attribution, as HTML.
    /// </summary>
    public string TileAttribution { get; set; } = DefaultTileAttribution;

    /// <summary>
    /// Gets or sets where maps open when there's nothing to show yet.
    /// </summary>
    public double DefaultLatitude { get; set; } = 39.5;

    /// <summary>
    /// Gets or sets where maps open when there's nothing to show yet.
    /// </summary>
    public double DefaultLongitude { get; set; } = -98.35;

    /// <summary>
    /// Gets or sets the zoom maps open at when there's nothing to show yet.
    /// </summary>
    public int DefaultZoom { get; set; } = 4;
}
