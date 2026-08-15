using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class StripeCheckoutTests
{
    [Fact]
    public void IsEligible_WhenNoSubscriptionLineItems_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            LineItems =
            [
                new InvoiceLineItem { Id = "one-time", Quantity = 1, UnitPrice = 10, Subscription = null },
            ],
        };

        var eligible = StripeCheckoutRequestFactory.IsEligible(invoice, out var reason);

        Assert.False(eligible);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void IsEligible_WithSingleBillingInterval_ReturnsTrue()
    {
        var invoice = new Invoice
        {
            LineItems =
            [
                Subscription("plan-a", 10, DurationType.Month, 1),
                Subscription("plan-b", 20, DurationType.Month, 1),
            ],
        };

        var eligible = StripeCheckoutRequestFactory.IsEligible(invoice, out var reason);

        Assert.True(eligible);
        Assert.Null(reason);
    }

    [Fact]
    public void IsEligible_WithMultipleBillingIntervals_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            LineItems =
            [
                Subscription("plan-monthly", 10, DurationType.Month, 1),
                Subscription("plan-yearly", 100, DurationType.Year, 1),
            ],
        };

        var eligible = StripeCheckoutRequestFactory.IsEligible(invoice, out var reason);

        Assert.False(eligible);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void IsEligible_WithUpFrontFee_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            InitialPaymentAmount = 25,
            LineItems =
            [
                Subscription("plan-a", 10, DurationType.Month, 1),
            ],
        };

        var eligible = StripeCheckoutRequestFactory.IsEligible(invoice, out var reason);

        Assert.False(eligible);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void Create_PopulatesRequestForHostedSubscriptionCheckout()
    {
        var lineItems = new[]
        {
            new CreateCheckoutLineItem { PriceId = "price_1", Quantity = 2 },
        };

        var request = StripeCheckoutRequestFactory.Create(
            sessionId: "session-123",
            lineItems: lineItems,
            successUrl: "https://example.test/success",
            cancelUrl: "https://example.test/cancel");

        Assert.Equal("subscription", request.Mode);
        Assert.Equal("hosted_page", request.UiMode);
        Assert.Equal("session-123", request.ClientReferenceId);
        Assert.Equal("https://example.test/success", request.SuccessUrl);
        Assert.Equal("https://example.test/cancel", request.CancelUrl);

        Assert.Equal("session-123", Assert.Contains(StripeCheckoutRequestFactory.SessionMetadataKey, request.Metadata));
        Assert.Equal("session-123", Assert.Contains(StripeCheckoutRequestFactory.SessionMetadataKey, request.SubscriptionMetadata));

        var item = Assert.Single(request.LineItems);
        Assert.Equal("price_1", item.PriceId);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Create_IgnoresLineItemsWithoutPriceOrQuantity()
    {
        var lineItems = new[]
        {
            new CreateCheckoutLineItem { PriceId = "price_1", Quantity = 1 },
            new CreateCheckoutLineItem { PriceId = "", Quantity = 3 },
            new CreateCheckoutLineItem { PriceId = "price_2", Quantity = 0 },
        };

        var request = StripeCheckoutRequestFactory.Create(
            sessionId: "session-123",
            lineItems: lineItems,
            successUrl: "https://example.test/success",
            cancelUrl: "https://example.test/cancel");

        var item = Assert.Single(request.LineItems);
        Assert.Equal("price_1", item.PriceId);
    }

    [Fact]
    public void Create_WhenNoValidLineItems_Throws()
    {
        var lineItems = new[]
        {
            new CreateCheckoutLineItem { PriceId = "", Quantity = 1 },
        };

        Assert.Throws<ArgumentException>(() => StripeCheckoutRequestFactory.Create(
            sessionId: "session-123",
            lineItems: lineItems,
            successUrl: "https://example.test/success",
            cancelUrl: "https://example.test/cancel"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WhenSessionIdMissing_Throws(string sessionId)
    {
        var lineItems = new[]
        {
            new CreateCheckoutLineItem { PriceId = "price_1", Quantity = 1 },
        };

        Assert.ThrowsAny<ArgumentException>(() => StripeCheckoutRequestFactory.Create(
            sessionId: sessionId,
            lineItems: lineItems,
            successUrl: "https://example.test/success",
            cancelUrl: "https://example.test/cancel"));
    }

    [Theory]
    [InlineData("complete", "paid", true)]
    [InlineData("complete", "no_payment_required", true)]
    [InlineData("complete", "unpaid", false)]
    [InlineData("open", "paid", false)]
    [InlineData("expired", "unpaid", false)]
    [InlineData(null, null, false)]
    public void CheckoutSessionDetails_IsPaid_ReflectsStatusAndPaymentStatus(string status, string paymentStatus, bool expected)
    {
        var details = new CheckoutSessionDetails
        {
            Status = status,
            PaymentStatus = paymentStatus,
        };

        Assert.Equal(expected, details.IsPaid);
    }

    private static InvoiceLineItem Subscription(string id, double unitPrice, DurationType durationType, int billingDuration)
        => new()
        {
            Id = id,
            Description = id,
            Quantity = 1,
            UnitPrice = unitPrice,
            Subscription = new SubscriptionPlan
            {
                DurationType = durationType,
                BillingDuration = billingDuration,
            },
        };
}
