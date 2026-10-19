using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Shipping;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Shipping",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ShippingConstants.Features.Shipping,
    Name = "Shipping",
    Description = "Charges for shipping at checkout, by zone, with flat, table and free rates from product weights and shipping classes, under Commerce > Shipping.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        OrderConstants.Features.Orders,
        AddressConstants.Feature.ModuleId,
        "OrchardCore.Contents",
    ]
)]

[assembly: Feature(
    Id = ShippingConstants.Features.EasyPost,
    Name = "EasyPost",
    Description = "Quotes UPS, USPS, FedEx, DHL and other carriers' rates at checkout, buys and prints their labels from an order's shipments, and follows the parcels, through an EasyPost account.",
    Category = "Commerce",
    Dependencies =
    [
        ShippingConstants.Features.Shipping,
    ]
)]
