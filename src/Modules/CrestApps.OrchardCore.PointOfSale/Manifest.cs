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
    Description = "Sells at your stores' counters from a phone or tablet app over the commerce API: registers at each location, shifts with their cash, sales paid in cash, by card, gift card or a mix, receipts, and returns at the till. Each sale is an order of its store, so its stock comes off that store and reports show sales by store and channel.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
        InventoryConstants.Features.Inventory,
        CheckoutConstants.Features.Api,
    ]
)]
