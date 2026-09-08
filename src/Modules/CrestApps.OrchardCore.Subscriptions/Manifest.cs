using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Subscriptions",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Subscriptions",
    Id = SubscriptionConstants.Features.Area,
    Description = "Sells recurring plans through the checkout and keeps a durable agreement for every subscriber.",
    Category = "Subscriptions",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "CrestApps.OrchardCore.Users",
        ProductConstants.Feature.ModuleId,

        // Subscriptions never collects money itself. Checkout is a hard dependency because it owns the
        // only path that takes a payment and the only ledger that records one.
        CheckoutConstants.Features.Area,
        ReceiptsConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - Sites",
    Id = SubscriptionConstants.Features.Tenants,
    Description = "Sells Orchard Core sites through the public checkout, creating each one from a durable job that survives a restart and retries on failure.",
    Category = "Subscriptions",
    DefaultTenantOnly = true,
    Dependencies =
    [
        SubscriptionConstants.Features.Area,

        // Tenants brings the setup services that create a site.
        "OrchardCore.Tenants",
    ]
)]
