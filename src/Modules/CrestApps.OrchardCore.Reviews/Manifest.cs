using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Reviews;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Product Reviews",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ReviewsConstants.Feature,
    Name = "Product Reviews",
    Description = "Lets buyers rate and review the products they bought, moderated by the store under Commerce > Reviews, with stars on product pages and an average rating listings can filter and sort by.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        OrderConstants.Features.Orders,
        "OrchardCore.Contents",
    ]
)]
