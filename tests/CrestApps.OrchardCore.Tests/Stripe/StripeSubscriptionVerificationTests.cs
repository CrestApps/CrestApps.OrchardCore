using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A recurring obligation is settled by a Stripe subscription rather than a single charge, so verification
/// has to read the subscription back. These tests pin the one thing that must never go wrong: an agreement
/// is only treated as paid when Stripe says its first invoice actually collected.
/// </summary>
public sealed class StripeSubscriptionVerificationTests
{
    [Fact]
    public async Task VerifyAsync_WhenTheFirstInvoiceIsPaid_Succeeds()
    {
        // Arrange
        var provider = CreateProvider(new SubscriptionDetails
        {
            Id = "sub_1",
            Status = "active",
            Currency = "USD",
            LatestInvoicePaid = true,
            AmountPaid = 22m,
            LatestPaymentId = "pi_1",
            LiveMode = true,
        });

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.True(result.ReportsAuthoritativeAmount);
        Assert.Equal("pi_1", result.TransactionId);

        // The tax is the immutable amount the checkout captured, so the net is the gross Stripe collected
        // minus that tax.
        Assert.Equal(20m, result.Amount);
        Assert.Equal(2m, result.TaxAmount);
        Assert.Equal(GatewayMode.Live, result.GatewayMode);
    }

    /// <summary>
    /// A trial establishes a real agreement with nothing collected yet. Leaving it outstanding would stall a
    /// checkout the customer completed correctly.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_ForATrial_SucceedsWithNothingCollected()
    {
        // Arrange
        var provider = CreateProvider(new SubscriptionDetails
        {
            Id = "sub_1",
            Status = "trialing",
            Currency = "USD",
            LatestInvoicePaid = false,
            AmountPaid = 0m,
        });

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.Equal("sub_1", result.TransactionId);

        // Nothing was collected, so nothing may be reported as charged. Subtracting the expected tax from a
        // zero gross would settle the obligation for a negative amount.
        Assert.Equal(0m, result.Amount);
        Assert.Equal(0m, result.TaxAmount);
    }

    /// <summary>
    /// An "active" subscription whose invoice is still open has collected nothing. Treating it as paid would
    /// hand out a subscription for free.
    /// </summary>
    [Theory]
    [InlineData("incomplete")]
    [InlineData("past_due")]
    [InlineData("unpaid")]
    [InlineData("active")]
    public async Task VerifyAsync_WhenTheFirstInvoiceIsUnpaid_StaysOutstanding(string status)
    {
        // Arrange
        var provider = CreateProvider(new SubscriptionDetails
        {
            Id = "sub_1",
            Status = status,
            Currency = "USD",
            LatestInvoicePaid = false,
        });

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Unknown, result.Status);
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("incomplete_expired")]
    public async Task VerifyAsync_WhenTheAgreementIsDead_Fails(string status)
    {
        // Arrange
        var provider = CreateProvider(new SubscriptionDetails
        {
            Id = "sub_1",
            Status = status,
            Currency = "USD",
            LatestInvoicePaid = false,
        });

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Failed, result.Status);
    }

    /// <summary>
    /// A transient read failure is not an authoritative outcome. Reporting failure would abandon a checkout
    /// whose payment may have gone through.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_WhenStripeIsUnreachable_StaysOutstanding()
    {
        // Arrange
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.GetAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Stripe is unreachable."));

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Unknown, result.Status);
    }

    /// <summary>
    /// Rolling back a checkout must end the agreement, not merely stop tracking it, or the customer keeps
    /// being billed for something they never received.
    /// </summary>
    [Fact]
    public async Task CancelAsync_ForASubscriptionReference_CancelsTheAgreement()
    {
        // Arrange
        CancelSubscriptionRequest captured = null;

        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CancelAsync(It.IsAny<CancelSubscriptionRequest>()))
            .ReturnsAsync((CancelSubscriptionRequest request) =>
            {
                captured = request;

                return new SubscriptionDetails { Id = "sub_1" };
            });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.CancelAsync(new CancelPaymentContext { Attempt = CreateAttempt() }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("sub_1", captured.SubscriptionId);
        Assert.False(captured.AtPeriodEnd);
        Assert.Equal("cancel_attempt-1", captured.IdempotencyKey);
    }

    private static PaymentAttempt CreateAttempt()
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = StripeConstants.ProcessorKey,
            ObligationId = "recurring",

            // A Stripe subscription id is what tells the adapter this obligation is an agreement rather than
            // a single charge.
            ProviderReference = "sub_1",
            Currency = "USD",
            ExpectedAmount = 20m,
            ExpectedTaxAmount = 2m,
        };

    private static StripeCheckoutPaymentProvider CreateProvider(SubscriptionDetails details)
    {
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(details);

        return CreateProvider(subscriptionService.Object);
    }

    private static StripeCheckoutPaymentProvider CreateProvider(IStripeSubscriptionService subscriptionService)
        => new(
            Mock.Of<IStripePaymentIntentService>(),
            subscriptionService,
            Mock.Of<IStripeRefundService>(),
            new StripeCheckoutCustomerResolver(Mock.Of<IStripeCustomerService>()),
            new InMemoryPaymentRefundStore(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());
}
