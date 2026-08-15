using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Drivers.Steps;
using CrestApps.OrchardCore.Stripe.Core;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class PaymentStepGuardTests
{
    [Theory]
    [InlineData(19.99, null)]
    [InlineData(null, 10.00)]
    [InlineData(19.99, 10.00)]
    public void PaymentIsRequired_WhenAnyAmountIsDue_ReturnsTrue(double? initial, double? recurring)
    {
        var invoice = new Invoice
        {
            InitialPaymentAmount = initial,
            FirstSubscriptionPaymentAmount = recurring,
        };

        Assert.True(PaymentStepSubscriptionFlowDisplayDriver.PaymentIsRequired(invoice));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0d, 0d)]
    [InlineData(0d, null)]
    [InlineData(null, 0d)]
    public void PaymentIsRequired_WhenNothingIsDue_ReturnsFalse(double? initial, double? recurring)
    {
        var invoice = new Invoice
        {
            InitialPaymentAmount = initial,
            FirstSubscriptionPaymentAmount = recurring,
        };

        Assert.False(PaymentStepSubscriptionFlowDisplayDriver.PaymentIsRequired(invoice));
    }

    [Fact]
    public void RequiresUnavailablePaymentProvider_WhenPaymentDueAndNoMethods_ReturnsTrue()
    {
        var invoice = new Invoice
        {
            InitialPaymentAmount = 19.99,
        };

        var options = new PaymentMethodOptions();

        Assert.Empty(options.PaymentMethods);
        Assert.True(PaymentStepSubscriptionFlowDisplayDriver.RequiresUnavailablePaymentProvider(invoice, options));
    }

    [Fact]
    public void RequiresUnavailablePaymentProvider_WhenPaymentDueAndStripeAvailable_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            InitialPaymentAmount = 19.99,
        };

        var options = new PaymentMethodOptions();
        options.PaymentMethods[StripeConstants.ProcessorKey] = new PaymentMethod
        {
            Title = "Credit Card",
            HasProcessor = true,
        };

        Assert.False(PaymentStepSubscriptionFlowDisplayDriver.RequiresUnavailablePaymentProvider(invoice, options));
    }

    [Fact]
    public void RequiresUnavailablePaymentProvider_WhenPaymentDueAndOnlyPayLaterAvailable_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            FirstSubscriptionPaymentAmount = 10.00,
        };

        var options = new PaymentMethodOptions();

        // Pay Later has no processor, yet it is still a valid way to complete a paid subscription
        // (it records the commitment without collecting money now), so it must not be blocked.
        options.PaymentMethods["PayLater"] = new PaymentMethod
        {
            Title = "Pay Later",
            HasProcessor = false,
        };

        Assert.False(PaymentStepSubscriptionFlowDisplayDriver.RequiresUnavailablePaymentProvider(invoice, options));
    }

    [Fact]
    public void RequiresUnavailablePaymentProvider_WhenFreePlanAndNoMethods_ReturnsFalse()
    {
        // A completely free plan requires no payment provider at all.
        var invoice = new Invoice();

        var options = new PaymentMethodOptions();

        Assert.False(PaymentStepSubscriptionFlowDisplayDriver.RequiresUnavailablePaymentProvider(invoice, options));
    }
}
