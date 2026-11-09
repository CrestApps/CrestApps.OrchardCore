using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;
using static CrestApps.OrchardCore.Geo.Recipes.GeoSchemaBuilder;

namespace CrestApps.OrchardCore.Geo.Recipes;

/// <summary>
/// Describes the <c>GeoSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class GeoSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(GeoSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => Object(
            "The site's map, geocoding and travel-time settings.",
            (nameof(GeoSettings.GeocodingProvider), Provider("The geocoding provider; null for none.")),
            (nameof(GeoSettings.TravelTimeProvider), Provider("The travel-time provider; null for the built-in estimate from AverageSpeedKmh and RoadFactor.")),
            (nameof(GeoSettings.RouteProvider), Provider("The route provider; null to draw straight lines.")),
            (nameof(GeoSettings.AverageSpeedKmh), Number("The average speed of the built-in estimate, in km/h. Defaults to 40.")),
            (nameof(GeoSettings.RoadFactor), Number("How much longer roads are than a straight line, for the built-in estimate. Defaults to 1.3.")),
            (nameof(GeoSettings.MatrixCacheMinutes), Integer("How long travel times are cached, in minutes. Defaults to 60.")),
            (nameof(GeoSettings.TileUrl), Text("The map tile address, with {z}, {x} and {y}. Defaults to the OpenStreetMap tile server, for trying things out only.")),
            (nameof(GeoSettings.TileAttribution), Text("The map's attribution, as HTML.")),
            (nameof(GeoSettings.DefaultLatitude), Number("The latitude maps open at when there's nothing to show yet. Defaults to 39.5.")),
            (nameof(GeoSettings.DefaultLongitude), Number("The longitude maps open at when there's nothing to show yet. Defaults to -98.35.")),
            (nameof(GeoSettings.DefaultZoom), Integer("The zoom maps open at when there's nothing to show yet. Defaults to 4.")));

    private static JsonSchemaBuilder Provider(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Examples("GoogleMaps", "AzureMaps", "Osrm")
            .Description($"{description} The name of a provider whose feature is on, such as GoogleMaps, AzureMaps or Osrm (OSRM gives travel times and routes only).");
}
