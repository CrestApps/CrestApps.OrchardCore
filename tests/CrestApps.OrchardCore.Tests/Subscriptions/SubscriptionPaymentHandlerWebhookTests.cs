using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Handlers;
using Moq;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class SubscriptionPaymentHandlerWebhookTests
{
    private const string Currency = "USD";

    // Payment gateways such as Stripe deliver webhooks with at-least-once semantics, so the same
    // 'subscription_create' notification can arrive multiple times. The handler must be idempotent:
    // it should neither double-count the amount nor lose the transaction id / 'Succeeded' status that
    // the completion validation later relies on.
    [Fact]
    public async Task PaymentSucceeded_DuplicateWebhookDelivery_IsIdempotent()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();

        var session = new SubscriptionSession
        {
            SessionId = "session-webhook-1",
            Status = SubscriptionSessionStatus.Pending,
        };

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        sessionStore.Setup(s => s.GetAsync(session.SessionId)).ReturnsAsync(session);

        var stripeService = new Mock<IStripePaymentIntentService>();

        var handler = new SubscriptionPaymentHandler(
            paymentSession,
            stripeService.Object,
            sessionStore.Object);

        var context = CreateContext(session.SessionId, subscriptionId: "sub_1", transactionId: "in_1", amount: 30.00);

        // Simulate the same webhook being delivered twice.
        await handler.PaymentSucceededAsync(context);
        await handler.PaymentSucceededAsync(context);

        var payments = await paymentSession.GetSubscriptionPaymentInfoAsync(session.SessionId);

        Assert.NotNull(payments);

        // Keyed by subscription id, so a duplicate delivery overwrites rather than appends.
        Assert.Single(payments.Payments);
        Assert.True(payments.Payments.ContainsKey("sub_1"));

        var payment = payments.Payments["sub_1"];

        // The amount must not be doubled by the second delivery.
        Assert.Equal(30.00, payment.Amount, 2);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("in_1", payment.TransactionId);
        Assert.Equal(Currency, payment.Currency);

        // The early-return path (no Stripe metadata on the session) must not attempt to confirm a payment intent.
        stripeService.Verify(
            s => s.ConfirmAsync(It.IsAny<CrestApps.OrchardCore.Stripe.Core.Models.ConfirmPaymentIntentRequest>()),
            Times.Never);
    }

    private static PaymentSucceededContext CreateContext(string sessionId, string subscriptionId, string transactionId, double amount)
    {
        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCreate,
            TransactionId = transactionId,
            AmountPaid = amount,
            Currency = Currency,
            GatewayId = "stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = subscriptionId,
            },
        };

        context.Data["sessionId"] = sessionId;

        return context;
    }
}
