using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Time Zones",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Category = "Infrastructure",
    Description = "Provides friendly named time zone maps and time zone picker services."
)]

[assembly: Feature(
    Name = "Time Zones",
    Id = CrestApps.OrchardCore.TimeZones.TimeZonesConstants.Features.Area,
    Category = "Infrastructure",
    Description = "Provides friendly named time zone maps, picker services, recipe import, and deployment export support.",
    Dependencies =
    [
        "OrchardCore.Recipes.Core",
    ]
)]
