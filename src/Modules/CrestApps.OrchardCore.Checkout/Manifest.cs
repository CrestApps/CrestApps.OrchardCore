using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Checkout",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Checkout",
    Id = CheckoutConstants.Features.Area,
    Description = "Provides a provider-agnostic checkout and payment framework reusable by subscriptions and one-time purchases.",
    Category = "Commerce"
)]
