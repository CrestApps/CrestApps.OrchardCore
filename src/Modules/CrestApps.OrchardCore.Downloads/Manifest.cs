using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Digital Downloads",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DownloadsConstants.Feature,
    Name = "Digital Downloads",
    Description = "Delivers digital products: files kept privately on each product, downloaded by buyers through expiring links with a download limit, on their order page and in the confirmation email.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        OrderConstants.Features.Orders,
        "OrchardCore.Contents",
    ]
)]
