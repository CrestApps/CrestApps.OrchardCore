using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Carts",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CartConstants.Features.Carts,
    Name = "Carts",
    Description = "Lets visitors add products sold online to a cart from listings and product pages. Enable Orders to check the cart out.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
        "OrchardCore.Contents",
    ]
)]

[assembly: Feature(
    Id = CartConstants.Features.Api,
    Name = "Cart API",
    Description = "Lets a headless storefront or app read and change the caller's cart over the commerce JSON API. A guest's cart travels in the X-Cart-Token header. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        CartConstants.Features.Carts,
    ]
)]
