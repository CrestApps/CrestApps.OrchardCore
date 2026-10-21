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

[assembly: Feature(
    Id = InventoryConstants.Features.StockAlerts,
    Name = "Back-in-Stock Alerts",
    Description = "Lets shoppers ask to be emailed when a product that's sold out is back in stock, and emails them, once, when it is.",
    Category = "Commerce",
    Dependencies =
    [
        InventoryConstants.Features.Inventory,
        "OrchardCore.Email",
    ]
)]

[assembly: Feature(
    Id = InventoryConstants.Features.PurchaseOrders,
    Name = "Purchase Orders",
    Description = "Orders stock from suppliers and receives it at a location, under Commerce > Purchase orders.",
    Category = "Commerce",
    Dependencies =
    [
        InventoryConstants.Features.Inventory,
        "CrestApps.OrchardCore.Resources",
    ]
)]
