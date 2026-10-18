using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
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
    Category = "Content Management",
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = ProductConstants.Feature.Recommendations,
    Name = "Product Recommendations",
    Description = "Recommends products to shoppers: the upsells and cross-sells you choose for each product, related products from the same categories, and the products a shopper viewed recently.",
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = ProductConstants.Feature.Listing,
    Name = "Product Listing",
    Description = "Lists products on the front end with filters, sorting, grid and list views, and product pages. Products can be listed without being sold online.",
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.ModuleId,
        "OrchardCore.Contents",
        "OrchardCore.Title",
        "OrchardCore.Autoroute",
    ]
)]
