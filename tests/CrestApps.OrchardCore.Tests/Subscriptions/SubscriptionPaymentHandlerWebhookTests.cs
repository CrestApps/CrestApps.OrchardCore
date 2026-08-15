using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Handlers;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.Entities;

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
            sessionStore.Object,
            new NullSubscriptionTaxService());

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

    // Regression: recurring 'subscription_cycle' renewal payments were previously dropped by an early
    // return that only allowed 'SubscriptionCreate'. Renewals must be recorded on the session and remain
    // idempotent under at-least-once webhook delivery.
    [Fact]
    public async Task PaymentSucceeded_SubscriptionCycle_RecordsRenewalPaymentIdempotently()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();

        var session = new SubscriptionSession
        {
            SessionId = "session-cycle-1",
            Status = SubscriptionSessionStatus.Completed,
        };

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        sessionStore.Setup(s => s.GetAsync(session.SessionId)).ReturnsAsync(session);

        var stripeService = new Mock<IStripePaymentIntentService>();

        var handler = new SubscriptionPaymentHandler(
            paymentSession,
            stripeService.Object,
            sessionStore.Object,
            new NullSubscriptionTaxService());

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_renew_1",
            AmountPaid = 30.00,
            Currency = Currency,
            GatewayId = "stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = "sub_1",
            },
        };
        context.Data["sessionId"] = session.SessionId;

        // Same renewal webhook delivered twice.
        await handler.PaymentSucceededAsync(context);
        await handler.PaymentSucceededAsync(context);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Single(metadata.Payments);
        Assert.True(metadata.Payments.ContainsKey("in_renew_1"));
        Assert.Equal(30.00, metadata.Payments["in_renew_1"].Amount, 2);
        Assert.Equal(PaymentStatus.Succeeded, metadata.Payments["in_renew_1"].Status);
        Assert.Equal("sub_1", metadata.Payments["in_renew_1"].SubscriptionId);

        sessionStore.Verify(s => s.SaveAsync(session), Times.AtLeastOnce);
        stripeService.Verify(
            s => s.ConfirmAsync(It.IsAny<CrestApps.OrchardCore.Stripe.Core.Models.ConfirmPaymentIntentRequest>()),
            Times.Never);
    }

    // Recurring cycle payments must record the tax redetermined for that cycle when taxation is enabled.
    [Fact]
    public async Task PaymentSucceeded_SubscriptionCycle_WithTaxation_RecordsTaxSnapshot()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();

        var session = new SubscriptionSession
        {
            SessionId = "session-cycle-tax-1",
            Status = SubscriptionSessionStatus.Completed,
        };

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        sessionStore.Setup(s => s.GetAsync(session.SessionId)).ReturnsAsync(session);

        var stripeService = new Mock<IStripePaymentIntentService>();

        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");
        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "CA Sales Tax",
            TaxType = TaxTypeNames.SalesTax,
            TaxName = "CA Sales Tax",
            TaxCode = "US-CA-SALES",
            JurisdictionId = jurisdictionId,
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.08m,
        });

        var taxService = new SubscriptionTaxService(
            harness.TaxService,
            harness.GetService<ITaxSnapshotFactory>(),
            new FixedSubscriptionTaxProfileProvider(new SubscriptionTaxProfile { Destination = TaxTestData.California() }),
            harness.Clock);

        var handler = new SubscriptionPaymentHandler(
            paymentSession,
            stripeService.Object,
            sessionStore.Object,
            taxService);

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_renew_tax_1",
            AmountPaid = 108.00,
            Currency = Currency,
            GatewayId = "stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = "sub_1",
            },
        };
        context.Data["sessionId"] = session.SessionId;

        await handler.PaymentSucceededAsync(context);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        var payment = metadata.Payments["in_renew_tax_1"];

        // The $108 charge is treated as tax-inclusive at 8%, so $8 is the embedded tax.
        Assert.Equal(8.00, payment.TaxAmount, 2);
        Assert.NotNull(payment.TaxSnapshot);
        Assert.Equal(8m, payment.TaxSnapshot.TaxAmount);
    }

    // Unrelated one-off payment reasons must be ignored so they do not pollute subscription history.
    [Fact]
    public async Task PaymentSucceeded_ManualReason_IsIgnored()
    {
        var paymentSession = PaymentTestHelpers.CreatePaymentSession();

        var sessionStore = new Mock<ISubscriptionSessionStore>();
        var stripeService = new Mock<IStripePaymentIntentService>();

        var handler = new SubscriptionPaymentHandler(
            paymentSession,
            stripeService.Object,
            sessionStore.Object,
            new NullSubscriptionTaxService());

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.Manual,
            TransactionId = "in_manual_1",
            AmountPaid = 10.00,
            Currency = Currency,
            GatewayId = "stripe",
        };
        context.Data["sessionId"] = "session-x";

        await handler.PaymentSucceededAsync(context);

        // A manual reason must not even attempt to load a session.
        sessionStore.Verify(s => s.GetAsync(It.IsAny<string>()), Times.Never);
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
