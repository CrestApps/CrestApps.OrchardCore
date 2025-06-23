using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Users.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Stripe",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Stripe",
    Id = StripeConstants.Feature.ModuleId,
    Description = "Provides Stripe integration for payment processing.",
    Category = "Payment Providers",
    Dependencies =
    [
        UsersConstants.Feature.Users,
    ]
)]
