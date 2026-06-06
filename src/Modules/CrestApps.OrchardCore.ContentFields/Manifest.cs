using CrestApps.OrchardCore;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps Content Fields",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Adds custom Orchard Core content fields maintained by CrestApps.",
    Category = "Content",
    IsAlwaysEnabled = false
)]

[assembly: Feature(
    Name = "CrestApps Content Fields",
    Id = "CrestApps.OrchardCore.ContentFields",
    Category = "Content",
    Description = "Adds custom Orchard Core content fields maintained by CrestApps.",
    Dependencies =
    [
        "CrestApps.OrchardCore.Resources",
        PhoneNumbersConstants.Features.Area,
        "OrchardCore.ContentFields",
        "OrchardCore.ContentTypes",
    ]
)]
