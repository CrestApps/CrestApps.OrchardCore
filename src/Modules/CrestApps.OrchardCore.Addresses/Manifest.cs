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
    Description = "Address formats for every country: which parts an address has, in what order, which are required, and how it is checked and printed.",
    Category = "Content Management"
)]

[assembly: Feature(
    Id = AddressConstants.Feature.Countries,
    Name = "Address Countries",
    Description = "Keeps the countries the site serves as Country content items, seeded from the ISO list, that administrators can rename, add or remove.",
    Category = "Content Management",
    Dependencies =
    [
        AddressConstants.Feature.ModuleId,
        "OrchardCore.Contents",
        "OrchardCore.ContentFields",
        "OrchardCore.Title",
    ]
)]
