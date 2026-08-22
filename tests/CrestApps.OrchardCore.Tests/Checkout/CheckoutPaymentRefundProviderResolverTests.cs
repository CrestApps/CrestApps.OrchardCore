using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutPaymentRefundProviderResolverTests
{
    [Fact]
    public void GetProvider_ReturnsRegisteredProviderByKey()
    {
        // Arrange
        var stripe = new FakeCheckoutPaymentRefundProvider("Stripe", _ => PaymentRefundResult.Failed("x", "y"));
        var resolver = new CheckoutPaymentRefundProviderResolver(
            [stripe],
            NullLogger<CheckoutPaymentRefundProviderResolver>.Instance);

        // Act
        var resolved = resolver.GetProvider("Stripe");

        // Assert
        Assert.Same(stripe, resolved);
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenKeyIsNotRegistered()
    {
        // Arrange
        var resolver = new CheckoutPaymentRefundProviderResolver(
            [],
            NullLogger<CheckoutPaymentRefundProviderResolver>.Instance);

        // Act
        var resolved = resolver.GetProvider("missing");

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void Resolver_KeepsFirstProvider_WhenDuplicateKeysAreRegistered()
    {
        // Arrange
        var first = new FakeCheckoutPaymentRefundProvider("Stripe", _ => PaymentRefundResult.Failed("x", "y"));
        var second = new FakeCheckoutPaymentRefundProvider("Stripe", _ => PaymentRefundResult.Failed("x", "y"));

        // Act
        var resolver = new CheckoutPaymentRefundProviderResolver(
            [first, second],
            NullLogger<CheckoutPaymentRefundProviderResolver>.Instance);

        // Assert
        Assert.Single(resolver.GetProviders());
        Assert.Same(first, resolver.GetProvider("Stripe"));
    }
}
