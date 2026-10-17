using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.PhoneNumbers.Core;
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
        PhoneNumberVerificationsConstants.Features.PhoneNumbers,
        "OrchardCore.ContentFields",
        "OrchardCore.ContentTypes",
    ]
)]

[assembly: Feature(
    Name = "Address Field",
    Id = "CrestApps.OrchardCore.ContentFields.Address",
    Category = "Content",
    Description = "Adds an address field that lays itself out and checks the address the way the chosen country writes addresses.",
    Dependencies =
    [
        AddressConstants.Feature.ModuleId,
        "OrchardCore.ContentFields",
        "OrchardCore.ContentTypes",
    ]
)]
