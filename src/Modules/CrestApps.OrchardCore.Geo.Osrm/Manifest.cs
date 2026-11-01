using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Geo;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "OSRM",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = GeoConstants.Features.Osrm,
    Name = "OSRM",
    Description = "Travel times and routes from a self-hosted OSRM (Open Source Routing Machine) server.",
    Category = "Geo",
    Dependencies =
    [
        GeoConstants.Features.Geo,
    ]
)]
