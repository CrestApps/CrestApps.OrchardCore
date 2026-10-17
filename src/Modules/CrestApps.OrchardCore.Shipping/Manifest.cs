using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Commerce;
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
    Description = "Charges for shipping by zone, with flat, table and free rates, from product weights and shipping classes, under Commerce > Shipping.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        AddressConstants.Feature.ModuleId,
        "OrchardCore.Contents",
    ]
)]
