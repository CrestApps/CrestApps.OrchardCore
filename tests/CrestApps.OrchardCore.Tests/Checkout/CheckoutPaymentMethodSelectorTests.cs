using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The payment step must offer only providers that can settle what the invoice owes. Offering one that cannot
/// is the failure that lets a customer select a method, submit, and then wait on a payment the framework was
/// never able to collect.
/// </summary>
public sealed class CheckoutPaymentMethodSelectorTests
{
    private static readonly string[] _oneTimeOnly = [CheckoutObligations.OneTime];
    private static readonly string[] _recurringOnly = ["recurring:1:1"];
    private static readonly string[] _both = [CheckoutObligations.OneTime, "recurring:1:1"];

    [Fact]
    public void GetEligible_ExcludesAProviderThatCannotSetUpRecurringPayments()
    {
        var oneTime = CreateProvider("one-time-only", supportsOneTime: true, supportsRecurring: false);
        var full = CreateProvider("full", supportsOneTime: true, supportsRecurring: true);

        var eligible = CheckoutPaymentMethodSelector.GetEligible([oneTime, full], _recurringOnly);

        Assert.Equal("full", Assert.Single(eligible).Key);
    }

    [Fact]
    public void GetEligible_ExcludesAProviderThatCannotCollectAOneTimeAmount()
    {
        var recurringOnly = CreateProvider("recurring-only", supportsOneTime: false, supportsRecurring: true);
        var full = CreateProvider("full", supportsOneTime: true, supportsRecurring: true);

        var eligible = CheckoutPaymentMethodSelector.GetEligible([recurringOnly, full], _oneTimeOnly);

        Assert.Equal("full", Assert.Single(eligible).Key);
    }

    /// <summary>
    /// A provider that can do each separately but not together cannot settle an invoice that owes both in one
    /// interaction, so it must not be offered for that invoice.
    /// </summary>
    [Fact]
    public void GetEligible_ExcludesAProviderThatCannotCombineAnUpFrontFeeWithARecurringCharge()
    {
        var separate = CreateProvider("separate", supportsOneTime: true, supportsRecurring: true, supportsCombined: false);
        var combined = CreateProvider("combined", supportsOneTime: true, supportsRecurring: true, supportsCombined: true);

        var eligible = CheckoutPaymentMethodSelector.GetEligible([separate, combined], _both);

        Assert.Equal("combined", Assert.Single(eligible).Key);
    }

    /// <summary>
    /// A checkout that owes nothing places no demands on a provider, so every registered one remains valid.
    /// </summary>
    [Fact]
    public void GetEligible_WhenNothingIsOwed_KeepsEveryProvider()
    {
        var limited = CreateProvider("limited", supportsOneTime: false, supportsRecurring: false);

        var eligible = CheckoutPaymentMethodSelector.GetEligible([limited], []);

        Assert.Single(eligible);
    }

    [Fact]
    public void ResolveDefault_HonorsTheConfiguredDefaultWhenItIsEligible()
    {
        var offline = CreateProvider("offline", supportsOneTime: true, supportsRecurring: true);
        var card = CreateProvider("card", supportsOneTime: true, supportsRecurring: true, supportsEmbedded: true);

        Assert.Equal("offline", CheckoutPaymentMethodSelector.ResolveDefault([offline, card], "offline"));
    }

    /// <summary>
    /// A configured default that is not among the eligible providers must not be preselected, otherwise the
    /// customer's first click lands on a method that is about to be refused.
    /// </summary>
    [Fact]
    public void ResolveDefault_IgnoresAConfiguredDefaultThatIsNotEligible()
    {
        var card = CreateProvider("card", supportsOneTime: true, supportsRecurring: true, supportsEmbedded: true);

        Assert.Equal("card", CheckoutPaymentMethodSelector.ResolveDefault([card], "not-offered"));
    }

    [Fact]
    public void ResolveDefault_WithoutAConfiguredDefault_PrefersAGatewayOverAnOfflineMethod()
    {
        var offline = CreateProvider("offline", supportsOneTime: true, supportsRecurring: true);
        var card = CreateProvider("card", supportsOneTime: true, supportsRecurring: true, supportsEmbedded: true);

        Assert.Equal("card", CheckoutPaymentMethodSelector.ResolveDefault([offline, card], configuredDefault: null));
    }

    [Fact]
    public void ResolveDefault_WithNoEligibleProviders_ReturnsNull()
        => Assert.Null(CheckoutPaymentMethodSelector.ResolveDefault([], "anything"));

    private static ICheckoutPaymentProvider CreateProvider(
        string key,
        bool supportsOneTime,
        bool supportsRecurring,
        bool supportsCombined = true,
        bool supportsEmbedded = false)
    {
        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns(key);
        provider.SetupGet(p => p.DisplayName).Returns(key);
        provider.SetupGet(p => p.Capabilities).Returns(new PaymentProviderCapabilities
        {
            SupportsOneTimePayments = supportsOneTime,
            SupportsRecurringPayments = supportsRecurring,
            SupportsCombinedOneTimeAndRecurring = supportsCombined,
            SupportsEmbeddedElements = supportsEmbedded,
        });

        return provider.Object;
    }
}
