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
    Id = ProductConstants.Feature.Comparison,
    Name = "Product Comparison",
    Description = "Lets shoppers pick up to four products and compare their prices and specifications side by side.",
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
    ]
)]

[assembly: Feature(
    Id = ProductConstants.Feature.Bundles,
    Name = "Product Bundles",
    Description = "Sells bundles and kits made of other products at their own price, with their stock worked out from their parts and taken from each part when sold.",
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "CrestApps.OrchardCore.Resources",
    ]
)]

[assembly: Feature(
    Id = ProductConstants.Feature.Grouped,
    Name = "Grouped Products",
    Description = "Shows several products on one page, such as the pieces of a set, for shoppers to buy in the quantities they choose.",
    Category = "Content Management",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
        "CrestApps.OrchardCore.Resources",
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

[assembly: Feature(
    Id = ProductConstants.Feature.Feeds,
    Name = "Product Feeds",
    Description = "Publishes the store's products as a Google Merchant Center feed, which Meta and TikTok catalogs read too, and as CSV: prices in any market's currency, photos, availability, sale prices and variants, with a Product Feed part for brands and GTINs, and what Merchant Center would refuse shown before it does.",
    Category = "Commerce",
    Dependencies =
    [
        ProductConstants.Feature.ModuleId,
    ]
)]

[assembly: Feature(
    Id = ProductConstants.Feature.Api,
    Name = "Catalog API",
    Description = "Lists and gets products over the commerce JSON API, priced for the caller, with their variants, add-ons and stock. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        ProductConstants.Feature.Listing,
    ]
)]
