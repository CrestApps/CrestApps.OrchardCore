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
    Id = SubscriptionConstants.Features.ModuleId,
    Description = "Provides a way to process and manage subscriptions.",
    Category = "Subscriptions",
    Dependencies =
    [
        "OrchardCore.Contents",
        "OrchardCore.ContentTypes",
        ProductConstants.Feature.ModuleId,
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - Stripe",
    Id = SubscriptionConstants.Features.Stripe,
    Description = "Provides a way to pay subscriptions using Stripe.",
    Category = "Subscriptions",
    Dependencies =
    [
        SubscriptionConstants.Features.ModuleId,
        StripeConstants.Feature.ModuleId,
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - Pay Later",
    Id = SubscriptionConstants.Features.PayLater,
    Description = "Provides a way to pay subscriptions later.",
    Category = "Subscriptions",
    Dependencies =
    [
        SubscriptionConstants.Features.ModuleId,
    ]
)]

[assembly: Feature(
    Name = "Subscriptions - reCaptcha",
    Id = SubscriptionConstants.Features.ReCaptcha,
    Description = "Provides reCaptcha to the subscription process.",
    Category = "Subscriptions",
    Dependencies =
    [
        SubscriptionConstants.Features.ModuleId,
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
        SubscriptionConstants.Features.ModuleId,
        StripeConstants.Feature.ModuleId,
        // Tenants adds setup services.
        "OrchardCore.Tenants",
    ]
)]
