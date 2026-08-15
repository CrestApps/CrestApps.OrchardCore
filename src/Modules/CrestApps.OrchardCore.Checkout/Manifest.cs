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

[assembly: Feature(
    Name = "Checkout - Pay Later",
    Id = CheckoutConstants.Features.PayLater,
    Description = "Lets a checkout be completed with an offline pay-later commitment instead of an online payment.",
    Category = "Commerce",
    Dependencies = [CheckoutConstants.Features.Area]
)]

[assembly: Feature(
    Name = "Checkout - Taxation",
    Id = CheckoutConstants.Features.Taxation,
    Description = "Applies taxation-framework tax to checkout invoices and recurring charges.",
    Category = "Commerce",
    Dependencies = [CheckoutConstants.Features.Area, "CrestApps.OrchardCore.Taxation"]
)]
