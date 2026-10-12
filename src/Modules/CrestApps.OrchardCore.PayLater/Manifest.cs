using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater;
using CrestApps.OrchardCore.Transactions;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Pay Later",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Pay Later",
    Id = PayLaterConstants.Features.Area,
    Description = "Adds an offline pay-later option to the checkout framework for subscriptions and one-time purchases, and records outstanding balances as trackable transactions.",
    Category = "Commerce",
    Dependencies =
    [
        CheckoutConstants.Features.Area,
        TransactionsConstants.Features.Area,
    ]
)]
