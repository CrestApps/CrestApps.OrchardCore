using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Enhanced Roles",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Extends Orchard Core Roles module by providing other services like role-part.",
    Category = "Roles Core Services",
    Dependencies =
    [
        "OrchardCore.Roles",
    ]
)]
