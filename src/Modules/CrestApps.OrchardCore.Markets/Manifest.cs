using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Markets;
using CrestApps.OrchardCore.Products.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Markets",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MarketsConstants.Feature,
    Name = "Markets",
    Description = "Sells to several countries, each market with its own currency, exchange rate and price rounding, language, and prices with or without tax. Shoppers pick their market on the storefront, and see and pay prices in its currency.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        ProductConstants.Feature.ModuleId,
    ]
)]
