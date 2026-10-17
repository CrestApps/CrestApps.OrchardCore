using System.Linq;
using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Transactions;
using OrchardCore.Modules.Manifest;
using Xunit;
using ModuleFeatureAttribute = OrchardCore.Modules.FeatureAttribute;

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
        // routed into the strongly-typed template, throwing at render time wherever a plan's summary is shown.
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
    public void SubscriptionsModule_DoesNotDeclareRemovedFlowSubFeatures()
    {
        // The session-based signup flow was removed in favour of buying a plan through the Checkout
        // feature. Its two sub-features went with it: reCaptcha guarded a step that no longer exists, and
        // tenant onboarding was replaced by "Subscriptions - Sites", which provisions from a durable job
        // instead of inline during the request that took the money. Guard against either id coming back,
        // because reintroducing one would mean a second, unledgered way to subscribe.
        var featureIds = typeof(SubscriptionPartDisplayDriver).Assembly
            .GetCustomAttributes<FeatureAttribute>()
            .Select(attribute => attribute.Id)
            .ToArray();

        Assert.DoesNotContain("CrestApps.OrchardCore.Subscriptions.ReCaptcha", featureIds);
        Assert.DoesNotContain("CrestApps.OrchardCore.Subscriptions.TenantOnboarding", featureIds);
        Assert.Contains(SubscriptionConstants.Features.Tenants, featureIds);
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

    [Fact]
    public void InstallmentsFeature_NeedsNeitherSubscriptionsNorProducts()
    {
        // A club that lets families pay a season fee in installments sells no products and no subscription plans, so
        // the feature must be enabled without either.
        var featureAttribute = typeof(SubscriptionPartDisplayDriver).Assembly
            .GetCustomAttributes<FeatureAttribute>()
            .Single(attribute => attribute.Id == SubscriptionConstants.Features.Installments);

        Assert.DoesNotContain(SubscriptionConstants.Features.Area, featureAttribute.Dependencies);
        Assert.DoesNotContain(ProductConstants.Feature.ModuleId, featureAttribute.Dependencies);
        Assert.DoesNotContain("OrchardCore.Contents", featureAttribute.Dependencies);
        Assert.Contains(TransactionsConstants.Features.Area, featureAttribute.Dependencies);
        Assert.Contains(CheckoutConstants.Features.Area, featureAttribute.Dependencies);
        Assert.Contains("OrchardCore.Users", featureAttribute.Dependencies);
    }

    [Theory]
    [InlineData(typeof(InstallmentPlansController))]
    [InlineData(typeof(InstallmentPlanSettingsDisplayDriver))]
    [InlineData(typeof(InstallmentOptionsCheckoutFlowDisplayDriver))]
    public void InstallmentsFeature_ServicesDoNotRequireProducts(Type type)
    {
        // Products may be disabled when only installment plans are enabled, so nothing the feature builds may need one
        // of its services to be constructed.
        var productsAssembly = typeof(ProductConstants).Assembly;

        var parameters = type.GetConstructors().SelectMany(constructor => constructor.GetParameters());

        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Assembly == productsAssembly);
    }

    [Fact]
    public void InstallmentPlansController_BelongsToTheInstallmentsFeature()
    {
        var feature = typeof(InstallmentPlansController).GetCustomAttribute<ModuleFeatureAttribute>();

        Assert.NotNull(feature);
        Assert.Equal(SubscriptionConstants.Features.Installments, feature.FeatureName);
    }

    private static FeatureAttribute GetSubscriptionsAreaFeature()
    {
        var assembly = typeof(SubscriptionPartDisplayDriver).Assembly;

        return assembly
            .GetCustomAttributes<FeatureAttribute>()
            .FirstOrDefault(attribute => attribute.Id == SubscriptionConstants.Features.Area);
    }
}
