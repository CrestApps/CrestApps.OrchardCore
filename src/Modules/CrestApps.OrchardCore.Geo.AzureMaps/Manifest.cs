using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Geo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Azure Maps",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = GeoConstants.Features.AzureMaps,
    Name = "Azure Maps",
    Description = "Geocoding, travel times with traffic and routes from Azure Maps, with a subscription key kept encrypted.",
    Category = "Geo",
    Dependencies =
    [
        GeoConstants.Features.Geo,
    ]
)]
