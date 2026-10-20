using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Orders.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Marketplace",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MarketplaceConstants.Feature,
    Name = "Marketplace",
    Description = "Sells other sellers' products: vendors, the commission the store keeps on each of their sales, what they've earned held in escrow until it's released (after a hold, and once the order is fulfilled), and payouts, with refunds taken back from what they're owed.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
    ]
)]

[assembly: Feature(
    Id = MarketplaceConstants.StripeConnectFeature,
    Name = "Marketplace payouts with Stripe Connect",
    Description = "Pays vendors to their own Stripe accounts: each vendor sets one up through Stripe's onboarding from their vendor page, and what they've earned on an order is transferred to it from that order's payment.",
    Category = "Commerce",
    Dependencies =
    [
        MarketplaceConstants.Feature,
        "CrestApps.OrchardCore.Stripe",
    ]
)]
