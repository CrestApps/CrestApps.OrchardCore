using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.PointOfSale;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Point of Sale",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = PosConstants.Feature,
    Name = "Point of Sale",
    Description = "Sells at your stores' counters over the commerce API, from a till app or the web register: registers at each location, shifts with their cash, sales of products and of amounts by department, paid in cash, by card, gift card or a mix, receipts, and returns at the till. Each sale is an order of its store, so its stock comes off that store and reports show sales by store and channel.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
        InventoryConstants.Features.Inventory,
        CheckoutConstants.Features.Api,
    ]
)]

[assembly: Feature(
    Id = PosConstants.WebRegisterFeature,
    Name = "Point of Sale Web Register",
    Description = "Rings up sales in the browser at your stores' registers, with no till app: department keys for amounts sold without an item to track, products by SKU or barcode scanner, cash and card on the counter terminal, receipts, and opening, counting and closing the shift.",
    Category = "Commerce",
    Dependencies =
    [
        PosConstants.Feature,
    ]
)]
