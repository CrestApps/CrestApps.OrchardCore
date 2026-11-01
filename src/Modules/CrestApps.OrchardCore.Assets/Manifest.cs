using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Assets;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Assets",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AssetsConstants.Features.Assets,
    Name = "Assets",
    Description = "Equipment, vehicles, tools and units installed at customers: types with attributes and meters, a hierarchy, warranty and meter readings.",
    Category = "Field Service"
)]

[assembly: Feature(
    Id = AssetsConstants.Features.Fleet,
    Name = "Fleet",
    Description = "Books vehicles, equipment and tools on their own Equipment calendars, so they can't be double-booked, and keeps the stock a van carries at its own Inventory location.",
    Category = "Field Service",
    Dependencies =
    [
        AssetsConstants.Features.Assets,
        "CrestApps.OrchardCore.Calendars.Resources",
    ]
)]
