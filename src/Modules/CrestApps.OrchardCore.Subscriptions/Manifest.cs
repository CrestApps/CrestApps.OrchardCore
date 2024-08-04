using CrestApps.OrchardCore.Stripe.Core;
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
    Id = SubscriptionsConstants.Features.ModuleId,
    Description = "Provides a way to process and manage subscriptions.",
    Category = "Subscriptions"
)]

[assembly: Feature(
    Name = "Subscriptions - Stripe",
    Id = SubscriptionsConstants.Features.Stripe,
    Description = "Provides a way to pay subscriptions using Stripe.",
    Category = "Subscriptions",
    Dependencies =
    [
        StripeConstants.Feature.ModuleId,
    ]
)]
