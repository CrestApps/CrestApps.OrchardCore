using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Inventory",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = InventoryConstants.Features.Inventory,
    Name = "Inventory",
    Description = "Counts how many units of each product and variant are on hand, with a history of every change, under Commerce > Inventory.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        "OrchardCore.Contents",
    ]
)]
