using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Products.Core;
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
    Id = SubscriptionConstants.Features.Area,
    Description = "Provides a way to process and manage subscriptions.",
    Category = "Subscriptions",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        "OrchardCore.Title",
        "CrestApps.OrchardCore.Users",
        ProductConstants.Feature.ModuleId,
        CheckoutConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - reCaptcha",
    Id = SubscriptionConstants.Features.ReCaptcha,
    Description = "Provides reCaptcha to the subscription process.",
    Category = "Subscriptions",
    Dependencies =
    [
        SubscriptionConstants.Features.Area,
        "OrchardCore.ReCaptcha",
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - Tenant Onboarding",
    Id = SubscriptionConstants.Features.TenantOnboarding,
    Description = "Provides a way to onboard new tenants using subscriptions.",
    Category = "Subscriptions",
    DefaultTenantOnly = true,
    Dependencies =
    [
        SubscriptionConstants.Features.Area,
        StripeConstants.Feature.ModuleId,
        // Tenants adds setup services.
        "OrchardCore.Tenants",
    ]
)]
