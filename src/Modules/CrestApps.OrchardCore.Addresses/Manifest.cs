using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Addresses",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AddressConstants.Feature.ModuleId,
    Name = "Addresses",
    Description = "Provides country, region, county, city, and district content types with reusable address and selector parts.",
    Category = "Content Management",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentFields",
        "OrchardCore.Title"
    ]
)]
