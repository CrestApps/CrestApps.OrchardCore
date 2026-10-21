using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Commerce;
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
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Test payments",
    Id = CheckoutConstants.Features.TestProvider,
    Description = "Adds a test payment method that confirms every checkout payment without moving money, for development and automated tests. It is active only in the Development environment unless configuration allows it.",
    Category = "Payment Providers",
    Dependencies =
    [
        CheckoutConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Checkout API",
    Id = CheckoutConstants.Features.Api,
    Description = "Lets a headless storefront or app read a checkout, put codes on it, pay it with a provider's client SDK and complete it over the commerce JSON API. A guest's checkout travels in the X-Checkout-Token header. Enable OpenAPI to document it.",
    Category = "Commerce",
    Dependencies =
    [
        CheckoutConstants.Features.Area,
    ]
)]
