using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// The recurring adapter is where a subscription becomes a real agreement at a gateway. These tests pin the
/// behaviors that decide whether a customer is billed correctly forever after: the price is defined inline
/// so any amount can be sold, the idempotency key is deterministic so a retry cannot create a second
/// agreement, and an agreement is never created without a reusable payment method to bill.
/// </summary>
public sealed class StripeRecurringPaymentProviderTests
{
    [Fact]
    public async Task BeginRecurringAsync_CreatesTheSubscriptionWithAnInlinePrice()
    {
        // Arrange
        CreateSubscriptionRequest captured = null;

        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()))
            .ReturnsAsync((CreateSubscriptionRequest request) =>
            {
                captured = request;

                return new CreateSubscriptionResponse { Id = "sub_1", Status = "active" };
            });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.BeginRecurringAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("sub_1", result.ProviderReference);
        Assert.False(result.RequiresAction);

        Assert.NotNull(captured);
        Assert.Equal("cus_created", captured.CustomerId);
        Assert.Equal("pm_123", captured.PaymentMethodId);
        Assert.Equal("checkout_session_1_recurring_1000_usd", captured.IdempotencyKey);

        var lineItem = Assert.Single(captured.LineItems);

        Assert.Null(lineItem.PriceId);
        Assert.NotNull(lineItem.Price);

        // The gross the checkout computed, tax folded in, is what the agreement bills each cycle.
        Assert.Equal(22m, lineItem.Price.UnitAmount);
        Assert.Equal("USD", lineItem.Price.Currency);
        Assert.Equal("month", lineItem.Price.Interval);
        Assert.Equal(1, lineItem.Price.IntervalCount);
        Assert.Equal("Membership", lineItem.Price.ProductName);
    }

    /// <summary>
    /// The metadata is what lets a later Stripe notification be traced back to the checkout that created the
    /// agreement. Without it a webhook is an orphan.
    /// </summary>
    [Fact]
    public async Task BeginRecurringAsync_TagsTheSubscriptionWithTheCheckout()
    {
        // Arrange
        CreateSubscriptionRequest captured = null;

        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()))
            .ReturnsAsync((CreateSubscriptionRequest request) =>
            {
                captured = request;

                return new CreateSubscriptionResponse { Id = "sub_1", Status = "active" };
            });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        await provider.BeginRecurringAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("attempt-1", captured.Metadata["checkout_attempt_id"]);
        Assert.Equal("session-1", captured.Metadata["checkout_session_id"]);
        Assert.Equal("recurring", captured.Metadata["checkout_obligation_id"]);
    }

    /// <summary>
    /// Stripe cannot bill a future cycle without a reusable payment method, and only the browser can produce
    /// one. Creating the agreement anyway would leave a subscription that can never collect.
    /// </summary>
    [Fact]
    public async Task BeginRecurringAsync_WithoutAPaymentMethod_Fails()
    {
        // Arrange
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        var provider = CreateProvider(subscriptionService.Object);

        var context = CreateContext();

        context.ProviderData = null;

        // Act
        var result = await provider.BeginRecurringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        subscriptionService.Verify(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()), Times.Never);
    }

    /// <summary>
    /// A subscription Stripe reports as incomplete still needs the customer to authenticate the first
    /// invoice. Reporting no action required would leave the checkout polling a payment nobody can finish.
    /// </summary>
    [Fact]
    public async Task BeginRecurringAsync_WhenStripeNeedsAuthentication_RequiresAction()
    {
        // Arrange
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()))
            .ReturnsAsync(new CreateSubscriptionResponse
            {
                Id = "sub_1",
                Status = "incomplete",
                ClientSecret = "pi_secret",
            });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.BeginRecurringAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresAction);
        Assert.Equal("pi_secret", result.ClientSecret);
    }

    /// <summary>
    /// A gateway failure must be reported, not thrown, so the checkout can tell the customer rather than
    /// returning a server error.
    /// </summary>
    [Fact]
    public async Task BeginRecurringAsync_WhenStripeThrows_ReportsFailure()
    {
        // Arrange
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()))
            .ThrowsAsync(new InvalidOperationException("Your card was declined."));

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.BeginRecurringAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Your card was declined.", result.ErrorMessage);
    }

    /// <summary>
    /// Ending an agreement at the close of the paid period is what keeps the customer's remaining time. The
    /// date the caller needs back differs between the two cases, so the wrong one would revoke access early
    /// or late.
    /// </summary>
    [Fact]
    public async Task CancelRecurringAsync_AtPeriodEnd_ReturnsThePaidThroughDate()
    {
        // Arrange
        var periodEnd = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.CancelAsync(It.IsAny<CancelSubscriptionRequest>()))
            .ReturnsAsync(new SubscriptionDetails { Id = "sub_1", CurrentPeriodEndUtc = periodEnd });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.CancelRecurringAsync(new CancelRecurringPaymentContext
        {
            ProviderSubscriptionId = "sub_1",
            AtPeriodEnd = true,
            Reason = "The customer canceled.",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(periodEnd, result.EndsUtc);
    }

    [Fact]
    public async Task UpdateRecurringAsync_MapsTheProrationBehavior()
    {
        // Arrange
        UpdateSubscriptionRequest captured = null;

        var subscriptionService = new Mock<IStripeSubscriptionService>();
        subscriptionService
            .Setup(service => service.UpdateAsync(It.IsAny<UpdateSubscriptionRequest>()))
            .ReturnsAsync((UpdateSubscriptionRequest request) =>
            {
                captured = request;

                return new SubscriptionDetails { Id = "sub_1" };
            });

        var provider = CreateProvider(subscriptionService.Object);

        // Act
        var result = await provider.UpdateRecurringAsync(new UpdateRecurringPaymentContext
        {
            ProviderSubscriptionId = "sub_1",
            Amount = 30m,
            Currency = "USD",
            Quantity = 2,
            Interval = new BillingDurationKey(DurationType.Year, 1),
            Description = "Gold membership",
            Proration = RecurringProrationBehavior.AlwaysInvoice,
            IdempotencyKey = "update_1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("always_invoice", captured.ProrationBehavior);
        Assert.Equal(2, captured.Quantity);
        Assert.Equal("year", captured.Price.Interval);
        Assert.Equal(30m, captured.Price.UnitAmount);
        Assert.Equal("update_1", captured.IdempotencyKey);
    }

    /// <summary>
    /// Stripe has no concept of an interval outside its four. Coercing an unmappable one would silently bill
    /// the customer on a different schedule than they agreed to.
    /// </summary>
    [Fact]
    public async Task BeginRecurringAsync_WithAnUnsupportedInterval_Fails()
    {
        // Arrange
        var subscriptionService = new Mock<IStripeSubscriptionService>();
        var provider = CreateProvider(subscriptionService.Object);

        var context = CreateContext();

        context.Interval = new BillingDurationKey((DurationType)99, 1);

        // Act
        var result = await provider.BeginRecurringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);

        subscriptionService.Verify(service => service.CreateAsync(It.IsAny<CreateSubscriptionRequest>()), Times.Never);
    }

    private static BeginRecurringPaymentContext CreateContext()
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = "owner-1",
            Currency = "USD",
        };

        session.Put(new CheckoutContactInfo { DisplayName = "Ada Lovelace", Email = "ada@example.com" });

        return new BeginRecurringPaymentContext
        {
            Session = session,
            Attempt = new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ObligationId = "recurring",
                ProviderKey = StripeConstants.ProcessorKey,
                IdempotencyKey = "checkout_session_1_recurring_1000_usd",
                Currency = "USD",
                ExpectedAmount = 20m,
                ExpectedTaxAmount = 2m,
            },
            Invoice = new CheckoutInvoice { Currency = "USD" },
            Interval = new BillingDurationKey(DurationType.Month, 1),
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "membership",
                    Description = "Membership",
                    Quantity = 1,
                    UnitPrice = 20m,
                    Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 },
                }
            ],
            ProviderData = new Dictionary<string, string> { ["paymentMethodId"] = "pm_123" },
        };
    }

    private static StripeRecurringPaymentProvider CreateProvider(IStripeSubscriptionService subscriptionService)
    {
        var customerService = new Mock<IStripeCustomerService>();
        customerService
            .Setup(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync(new CreateCustomerResponse { CustomerId = "cus_created" });

        return new StripeRecurringPaymentProvider(
            subscriptionService,
            customerService.Object,
            NullLogger<StripeRecurringPaymentProvider>.Instance);
    }
}
