using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Geo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Google Maps",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = GeoConstants.Features.GoogleMaps,
    Name = "Google Maps",
    Description = "Geocoding, travel times with traffic and routes from Google Maps, with an API key kept encrypted. Travel times are not cached, as its terms require.",
    Category = "Geo",
    Dependencies =
    [
        GeoConstants.Features.Geo,
    ]
)]
