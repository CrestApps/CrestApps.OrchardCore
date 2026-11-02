using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Geo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Geo",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = GeoConstants.Features.Geo,
    Name = "Geo, Maps and Territories",
    Description = "Service territories drawn on a map or listed by postal code, geocoding addresses in the background, travel times with a built-in estimate when no provider is set up, and Leaflet maps.",
    Category = "Geo",
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
    ]
)]
