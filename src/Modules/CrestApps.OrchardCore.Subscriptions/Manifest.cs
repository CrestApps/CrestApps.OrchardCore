using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Transactions;
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

[assembly: Feature(
    Name = "Subscriptions - Installment Plans",
    Id = SubscriptionConstants.Features.Installments,
    Description = "Lets an administrator set up a payment plan for a customer: take a down payment by card, then collect a fixed number of scheduled payments, charged to the saved card or invoiced, with reminders and receipts.",
    Category = "Subscriptions",
    Dependencies =
    [
        SubscriptionConstants.Features.Area,

        // Every payment on a plan is a ledger transaction, so the customer sees and pays them like any other.
        TransactionsConstants.Features.Area,
        CheckoutConstants.Features.Area,
    ]
)]
