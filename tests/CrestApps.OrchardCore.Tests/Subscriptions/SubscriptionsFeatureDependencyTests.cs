using System.Linq;
using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using OrchardCore.Modules.Manifest;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class SubscriptionsFeatureDependencyTests
{
    [Fact]
    public void SubscriptionsFeature_DependsOnTitleFeature()
    {
        // The Subscriptions module ships a "Subscription-TitlePart.Summary" template that binds
        // to OrchardCore.Title.ViewModels.TitlePartViewModel. When the Title feature is disabled
        // but a subscription content type still carries a TitlePart, Orchard produces a generic
        // ContentPart fallback shape whose model is a ZoneHolding. Because of the "Subscription"
        // stereotype, that fallback matches the "Subscription_Summary__TitlePart" alternate and is
        // routed into the strongly-typed template, throwing at render time on the ServicePlans page.
        // Declaring OrchardCore.Title as a hard dependency guarantees the real TitlePart driver runs.
        var featureAttribute = GetSubscriptionsAreaFeature();

        Assert.NotNull(featureAttribute);
        Assert.Contains("OrchardCore.Title", featureAttribute.Dependencies);
    }

    [Fact]
    public void SubscriptionsFeature_DependsOnContentsAndProducts()
    {
        var featureAttribute = GetSubscriptionsAreaFeature();

        Assert.NotNull(featureAttribute);
        Assert.Contains("OrchardCore.Contents", featureAttribute.Dependencies);
        Assert.Contains(ProductConstants.Feature.ModuleId, featureAttribute.Dependencies);
    }

    [Fact]
    public void SubscriptionsFeature_DependsOnCheckout()
    {
        // Subscriptions cannot collect money on its own; it relies on the provider-agnostic Checkout
        // framework to contribute payment providers. Declaring Checkout as a hard dependency guarantees
        // the checkout services are present whenever Subscriptions is enabled.
        var featureAttribute = GetSubscriptionsAreaFeature();

        Assert.NotNull(featureAttribute);
        Assert.Contains(CheckoutConstants.Features.Area, featureAttribute.Dependencies);
    }

    [Fact]
    public void SubscriptionsModule_DoesNotDeclareRemovedIntegrationSubFeatures()
    {
        // The former "Subscriptions - Stripe" and "Subscriptions - Pay Later" sub-features were removed.
        // Stripe now activates via [RequireFeatures] when the Stripe module is enabled, and Pay Later is a
        // standalone module. Guard against either id being reintroduced as a separate feature.
        var assembly = typeof(SubscriptionPartDisplayDriver).Assembly;

        var featureIds = assembly
            .GetCustomAttributes<FeatureAttribute>()
            .Select(attribute => attribute.Id)
            .ToArray();

        Assert.DoesNotContain("CrestApps.OrchardCore.Subscriptions.Stripe", featureIds);
        Assert.DoesNotContain("CrestApps.OrchardCore.Subscriptions.PayLater", featureIds);
    }

    private static FeatureAttribute GetSubscriptionsAreaFeature()
    {
        var assembly = typeof(SubscriptionPartDisplayDriver).Assembly;

        return assembly
            .GetCustomAttributes<FeatureAttribute>()
            .FirstOrDefault(attribute => attribute.Id == SubscriptionConstants.Features.Area);
    }
}
