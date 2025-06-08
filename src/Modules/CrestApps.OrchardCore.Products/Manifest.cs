using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Products",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ProductConstants.Feature.ModuleId,
    Name = "Products",
    Description = "Provides product related components.",
    Category = "Content Management"
)]
