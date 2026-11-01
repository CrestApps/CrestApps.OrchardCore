using CrestApps.OrchardCore.Geo.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Geo.ViewModels;

/// <summary>
/// The map, geocoding and travel-time settings editor.
/// </summary>
public class GeoSettingsViewModel
{
    /// <summary>
    /// Gets or sets the geocoding provider.
    /// </summary>
    public string GeocodingProvider { get; set; }

    /// <summary>
    /// Gets or sets the travel-time provider.
    /// </summary>
    public string TravelTimeProvider { get; set; }

    /// <summary>
    /// Gets or sets the route provider.
    /// </summary>
    public string RouteProvider { get; set; }

    /// <summary>
    /// Gets or sets the average speed of the built-in estimate.
    /// </summary>
    public double AverageSpeedKmh { get; set; }

    /// <summary>
    /// Gets or sets the road factor of the built-in estimate.
    /// </summary>
    public double RoadFactor { get; set; }

    /// <summary>
    /// Gets or sets how long travel times are cached.
    /// </summary>
    public int MatrixCacheMinutes { get; set; }

    /// <summary>
    /// Gets or sets the tile address.
    /// </summary>
    public string TileUrl { get; set; }

    /// <summary>
    /// Gets or sets the tile attribution.
    /// </summary>
    public string TileAttribution { get; set; }

    /// <summary>
    /// Gets or sets where maps open.
    /// </summary>
    public double DefaultLatitude { get; set; }

    /// <summary>
    /// Gets or sets where maps open.
    /// </summary>
    public double DefaultLongitude { get; set; }

    /// <summary>
    /// Gets or sets the zoom maps open at.
    /// </summary>
    public int DefaultZoom { get; set; }

    /// <summary>
    /// Gets or sets how many addresses wait to be geocoded.
    /// </summary>
    [BindNever]
    public int Waiting { get; set; }

    /// <summary>
    /// Gets or sets how many addresses were geocoded.
    /// </summary>
    [BindNever]
    public int Done { get; set; }

    /// <summary>
    /// Gets or sets how many addresses were given up.
    /// </summary>
    [BindNever]
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the latest reason an address wasn't geocoded.
    /// </summary>
    [BindNever]
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the geocoding providers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> GeocodingProviders { get; set; } = [];

    /// <summary>
    /// Gets or sets the travel-time providers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TravelTimeProviders { get; set; } = [];

    /// <summary>
    /// Gets or sets the route providers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RouteProviders { get; set; } = [];
}

/// <summary>
/// A row of the territory list.
/// </summary>
public sealed class TerritoryRow
{
    /// <summary>
    /// Gets or sets the territory.
    /// </summary>
    public ServiceTerritory Territory { get; set; }

    /// <summary>
    /// Gets or sets the parent's name.
    /// </summary>
    public string ParentName { get; set; }
}

/// <summary>
/// The territory list, its map and the territory finder.
/// </summary>
public class TerritoryListViewModel
{
    /// <summary>
    /// Gets or sets the latitude to find the territory of.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude to find the territory of.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the postal code to find the territory of.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a search was made.
    /// </summary>
    [BindNever]
    public bool Searched { get; set; }

    /// <summary>
    /// Gets or sets the territory found.
    /// </summary>
    [BindNever]
    public TerritoryMatch Match { get; set; }

    /// <summary>
    /// Gets or sets the shapes to draw on the map.
    /// </summary>
    [BindNever]
    public IList<TerritoryShape> Shapes { get; set; } = [];

    /// <summary>
    /// Gets or sets the map settings.
    /// </summary>
    [BindNever]
    public GeoSettings Map { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A territory's shape for the overview map.
/// </summary>
public sealed class TerritoryShape
{
    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the GeoJSON.
    /// </summary>
    public string GeoJson { get; set; }
}

/// <summary>
/// The territory editor.
/// </summary>
public class TerritoryEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new territory.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the shape, as GeoJSON.
    /// </summary>
    public string Shape { get; set; }

    /// <summary>
    /// Gets or sets the postal codes, separated by commas or new lines.
    /// </summary>
    public string PostalCodes { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the travel speed.
    /// </summary>
    public double? TravelSpeedKmh { get; set; }

    /// <summary>
    /// Gets or sets the other territories, to draw around this one.
    /// </summary>
    [BindNever]
    public IList<TerritoryShape> OtherShapes { get; set; } = [];

    /// <summary>
    /// Gets or sets the possible parents.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ParentOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the map settings.
    /// </summary>
    [BindNever]
    public GeoSettings Map { get; set; }
}
