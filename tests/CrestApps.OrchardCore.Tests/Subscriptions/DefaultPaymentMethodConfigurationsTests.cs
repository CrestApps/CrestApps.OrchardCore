using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public sealed class DefaultPaymentMethodConfigurationsTests
{
    [Fact]
    public void PostConfigure_WhenOnlyPayLaterRegistered_SelectsPayLaterAsDefault()
    {
        // Arrange
        var options = new PaymentMethodOptions();
        options.PaymentMethods[SubscriptionConstants.PayLaterProcessorKey] = new PaymentMethod
        {
            Title = "Pay Later",
            HasProcessor = false,
        };

        var configuration = CreateConfiguration(new SubscriptionSettings());

        // Act
        configuration.PostConfigure(Options.DefaultName, options);

        // Assert
        // A tenant that enables only the offline Pay Later option still needs a preselected method so the
        // single-method checkout renders and completes without the shopper choosing anything.
        Assert.Equal(SubscriptionConstants.PayLaterProcessorKey, options.DefaultPaymentMethod);
    }

    [Fact]
    public void PostConfigure_WhenMultipleMethodsRegistered_PrefersMethodWithProcessor()
    {
        // Arrange
        var options = new PaymentMethodOptions();
        options.PaymentMethods[SubscriptionConstants.PayLaterProcessorKey] = new PaymentMethod
        {
            Title = "Pay Later",
            HasProcessor = false,
        };
        options.PaymentMethods[StripeConstants.ProcessorKey] = new PaymentMethod
        {
            Title = "Stripe",
            HasProcessor = true,
        };

        var configuration = CreateConfiguration(new SubscriptionSettings());

        // Act
        configuration.PostConfigure(Options.DefaultName, options);

        // Assert
        Assert.Equal(StripeConstants.ProcessorKey, options.DefaultPaymentMethod);
    }

    [Fact]
    public void PostConfigure_WhenSettingsSpecifyAvailableMethod_UsesConfiguredDefault()
    {
        // Arrange
        var options = new PaymentMethodOptions();
        options.PaymentMethods[SubscriptionConstants.PayLaterProcessorKey] = new PaymentMethod
        {
            Title = "Pay Later",
            HasProcessor = false,
        };
        options.PaymentMethods[StripeConstants.ProcessorKey] = new PaymentMethod
        {
            Title = "Stripe",
            HasProcessor = true,
        };

        var configuration = CreateConfiguration(new SubscriptionSettings
        {
            DefaultPaymentMethod = SubscriptionConstants.PayLaterProcessorKey,
        });

        // Act
        configuration.PostConfigure(Options.DefaultName, options);

        // Assert
        Assert.Equal(SubscriptionConstants.PayLaterProcessorKey, options.DefaultPaymentMethod);
    }

    [Fact]
    public void PostConfigure_WhenSettingsSpecifyUnavailableMethod_FallsBackToAvailableMethod()
    {
        // Arrange
        var options = new PaymentMethodOptions();
        options.PaymentMethods[StripeConstants.ProcessorKey] = new PaymentMethod
        {
            Title = "Stripe",
            HasProcessor = true,
        };

        var configuration = CreateConfiguration(new SubscriptionSettings
        {
            DefaultPaymentMethod = "a-method-that-is-no-longer-enabled",
        });

        // Act
        configuration.PostConfigure(Options.DefaultName, options);

        // Assert
        Assert.Equal(StripeConstants.ProcessorKey, options.DefaultPaymentMethod);
    }

    [Fact]
    public void PostConfigure_WhenNoMethodsRegistered_LeavesDefaultUnset()
    {
        // Arrange
        var options = new PaymentMethodOptions();

        var configuration = CreateConfiguration(new SubscriptionSettings());

        // Act
        configuration.PostConfigure(Options.DefaultName, options);

        // Assert
        Assert.Null(options.DefaultPaymentMethod);
    }

    private static DefaultPaymentMethodConfigurations CreateConfiguration(SubscriptionSettings settings)
        => new(SiteServiceFactory.Create(settings));
}
