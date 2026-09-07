using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
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
            new NullSubscriptionTaxService(),
            new TestClock(DateTime.UtcNow));

        var context = CreateContext(session.SessionId, subscriptionId: "sub_1", transactionId: "in_1", amount: 30.00m);

        // Simulate the same webhook being delivered twice.
        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);
        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        var payments = await paymentSession.GetSubscriptionPaymentInfoAsync(session.SessionId);

        Assert.NotNull(payments);

        // Keyed by subscription id, so a duplicate delivery overwrites rather than appends.
        Assert.Single(payments.Payments);
        Assert.True(payments.Payments.ContainsKey("sub_1"));

        var payment = payments.Payments["sub_1"];

        // The amount must not be doubled by the second delivery.
        Assert.Equal(30.00m, payment.Amount, 2);
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
            new NullSubscriptionTaxService(),
            new TestClock(DateTime.UtcNow));

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_renew_1",
            AmountPaid = 30.00m,
            Currency = Currency,
            GatewayId = "stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = "sub_1",
            },
        };
        context.Data["sessionId"] = session.SessionId;

        // Same renewal webhook delivered twice.
        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);
        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Single(metadata.Payments);
        Assert.True(metadata.Payments.ContainsKey("in_renew_1"));
        Assert.Equal(30.00m, metadata.Payments["in_renew_1"].Amount, 2);
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
            Source = TaxCalculationMethodNames.Percentage,
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
            taxService,
            harness.Clock);

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_renew_tax_1",
            AmountPaid = 108.00m,
            Currency = Currency,
            GatewayId = "stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = "sub_1",
            },
        };
        context.Data["sessionId"] = session.SessionId;

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        var payment = metadata.Payments["in_renew_tax_1"];

        // The $108 charge is treated as tax-inclusive at 8%, so $8 is the embedded tax.
        Assert.Equal(8.00m, payment.TaxAmount, 2);
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
            new NullSubscriptionTaxService(),
            new TestClock(DateTime.UtcNow));

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.Manual,
            TransactionId = "in_manual_1",
            AmountPaid = 10.00m,
            Currency = Currency,
            GatewayId = "stripe",
        };
        context.Data["sessionId"] = "session-x";

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        // A manual reason must not even attempt to load a session.
        sessionStore.Verify(s => s.GetAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A renewal buys another billing cycle, so the recorded expiration has to move forward. Without this every
    /// subscription looks like it expires at the end of its first cycle no matter how long the customer pays,
    /// which is what the expiring-subscriptions report and any access check read.
    /// </summary>
    [Fact]
    public async Task PaymentSucceeded_SubscriptionCycle_AdvancesTheExpirationByOneCycle()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var firstExpiration = start.AddMonths(1);

        var session = CreateSessionWithSubscription("session-renew-expiry", "sub_1", start, firstExpiration);
        var handler = CreateHandler(session, out _, new TestClock(firstExpiration));

        var context = CreateContext(session.SessionId, "sub_1", "in_renew_1", 30.00m);
        context.Reason = PaymentReason.SubscriptionCycle;

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        Assert.True(session.TryGet<SubscriptionsMetadata>(out var metadata));

        var subscription = Assert.Single(metadata.Subscriptions);

        Assert.Equal(start.AddMonths(2), subscription.ExpiresAt);
    }

    /// <summary>
    /// The expiration advances from the previous expiration rather than from the moment the webhook happened to
    /// be processed, so a renewal recorded late never shortens the period the customer paid for.
    /// </summary>
    [Fact]
    public async Task PaymentSucceeded_SubscriptionCycle_AdvancesFromTheExpirationNotFromNow()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var firstExpiration = start.AddMonths(1);

        var session = CreateSessionWithSubscription("session-renew-late", "sub_1", start, firstExpiration);

        // The renewal is only processed three days after the period ended.
        var handler = CreateHandler(session, out _, new TestClock(firstExpiration.AddDays(3)));

        var context = CreateContext(session.SessionId, "sub_1", "in_renew_late", 30.00m);
        context.Reason = PaymentReason.SubscriptionCycle;

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        Assert.Equal(start.AddMonths(2), Assert.Single(metadata.Subscriptions).ExpiresAt);
    }

    /// <summary>
    /// Every payment carries its own collection date so a renewal is reported and receipted in the month it was
    /// collected instead of the month the subscription was created.
    /// </summary>
    [Fact]
    public async Task PaymentSucceeded_SubscriptionCycle_RecordsTheCollectionDate()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var renewedAt = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

        var session = CreateSessionWithSubscription("session-renew-date", "sub_1", start, start.AddMonths(1));
        var handler = CreateHandler(session, out _, new TestClock(renewedAt));

        var context = CreateContext(session.SessionId, "sub_1", "in_renew_date", 30.00m);
        context.Reason = PaymentReason.SubscriptionCycle;

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        Assert.True(session.TryGet<PaymentsMetadata>(out var payments));
        Assert.Equal(renewedAt, payments.Payments["in_renew_date"].CreatedUtc);
    }

    /// <summary>
    /// A failed renewal must move the subscription to past due so the site can chase or restrict the customer,
    /// rather than the site only noticing that payments quietly stopped arriving.
    /// </summary>
    [Fact]
    public async Task SubscriptionPaymentFailed_MarksTheSubscriptionPastDue()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var failedAt = start.AddMonths(1);

        var session = CreateSessionWithSubscription("session-past-due", "sub_1", start, failedAt);
        var handler = CreateHandler(session, out var sessionStore, new TestClock(failedAt));

        var context = new SubscriptionPaymentFailedContext
        {
            SubscriptionId = "sub_1",
            TransactionId = "in_failed_1",
            Currency = Currency,
            GatewayId = "stripe",
        };

        context.Data["sessionId"] = session.SessionId;

        await handler.SubscriptionPaymentFailedAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        var subscription = Assert.Single(metadata.Subscriptions);

        Assert.Equal(SubscriptionLifecycleStatus.PastDue, subscription.Status);
        Assert.Equal(failedAt, subscription.PastDueSinceUtc);
        sessionStore.Verify(s => s.SaveAsync(session), Times.Once);
    }

    /// <summary>
    /// The dunning window is measured from the first failure, so a gateway's retry sequence must not keep
    /// resetting the date the subscription fell behind.
    /// </summary>
    [Fact]
    public async Task SubscriptionPaymentFailed_KeepsTheFirstFailureDateAcrossRetries()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var firstFailure = start.AddMonths(1);

        var session = CreateSessionWithSubscription("session-past-due-retry", "sub_1", start, firstFailure);
        var clock = new TestClock(firstFailure);
        var handler = CreateHandler(session, out _, clock);

        var context = new SubscriptionPaymentFailedContext
        {
            SubscriptionId = "sub_1",
            TransactionId = "in_failed_1",
            Currency = Currency,
            GatewayId = "stripe",
        };

        context.Data["sessionId"] = session.SessionId;

        await handler.SubscriptionPaymentFailedAsync(context, TestContext.Current.CancellationToken);

        clock.UtcNow = firstFailure.AddDays(3);

        await handler.SubscriptionPaymentFailedAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        Assert.Equal(firstFailure, Assert.Single(metadata.Subscriptions).PastDueSinceUtc);
    }

    /// <summary>
    /// A subscription canceled at the gateway (from its dashboard, or by the gateway after exhausting dunning)
    /// must be reflected locally, otherwise the site keeps treating a dead agreement as active.
    /// </summary>
    [Fact]
    public async Task SubscriptionStatusChanged_CancellationIsRecorded()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = start.AddMonths(1);
        var canceledAt = start.AddDays(10);

        var session = CreateSessionWithSubscription("session-canceled", "sub_1", start, periodEnd);
        var handler = CreateHandler(session, out _, new TestClock(canceledAt));

        var context = new SubscriptionStatusChangedContext
        {
            SubscriptionId = "sub_1",
            Status = RemoteSubscriptionStatus.Canceled,
            CanceledUtc = canceledAt,
            CurrentPeriodEndUtc = periodEnd,
            CancelAtPeriodEnd = true,
            GatewayId = "stripe",
        };

        context.Data["sessionId"] = session.SessionId;

        await handler.SubscriptionStatusChangedAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        var subscription = Assert.Single(metadata.Subscriptions);

        Assert.Equal(SubscriptionLifecycleStatus.Canceled, subscription.Status);
        Assert.Equal(canceledAt, subscription.CanceledAt);
        Assert.True(subscription.CancelAtPeriodEnd);

        // Canceling at period end still leaves the customer paid through the end of the period.
        Assert.Equal(periodEnd, subscription.ExpiresAt);
    }

    /// <summary>
    /// A status the adapter does not recognize is not a reason to change local state; guessing could revoke or
    /// grant access the gateway never actually reported.
    /// </summary>
    [Fact]
    public async Task SubscriptionStatusChanged_UnknownStatusLeavesTheSubscriptionAlone()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var session = CreateSessionWithSubscription("session-unknown", "sub_1", start, start.AddMonths(1));
        var handler = CreateHandler(session, out var sessionStore, new TestClock(start));

        var context = new SubscriptionStatusChangedContext
        {
            SubscriptionId = "sub_1",
            Status = RemoteSubscriptionStatus.Unknown,
            GatewayId = "stripe",
        };

        context.Data["sessionId"] = session.SessionId;

        await handler.SubscriptionStatusChangedAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        Assert.Equal(SubscriptionLifecycleStatus.Active, Assert.Single(metadata.Subscriptions).Status);
        sessionStore.Verify(s => s.SaveAsync(It.IsAny<SubscriptionSession>()), Times.Never);
    }

    /// <summary>
    /// A late-arriving payment failure for an already-canceled subscription must not resurrect it as past due,
    /// because past due implies the agreement still exists and can be recovered.
    /// </summary>
    [Fact]
    public async Task SubscriptionPaymentFailed_DoesNotResurrectACanceledSubscription()
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var session = CreateSessionWithSubscription("session-late-failure", "sub_1", start, start.AddMonths(1));

        session.TryGet<SubscriptionsMetadata>(out var seeded);
        seeded.Subscriptions[0].Status = SubscriptionLifecycleStatus.Canceled;
        session.Put(seeded);

        var handler = CreateHandler(session, out _, new TestClock(start.AddMonths(1)));

        var context = new SubscriptionPaymentFailedContext
        {
            SubscriptionId = "sub_1",
            TransactionId = "in_failed_late",
            Currency = Currency,
            GatewayId = "stripe",
        };

        context.Data["sessionId"] = session.SessionId;

        await handler.SubscriptionPaymentFailedAsync(context, TestContext.Current.CancellationToken);

        session.TryGet<SubscriptionsMetadata>(out var metadata);

        Assert.Equal(SubscriptionLifecycleStatus.Canceled, Assert.Single(metadata.Subscriptions).Status);
    }

    private static SubscriptionSession CreateSessionWithSubscription(
        string sessionId,
        string subscriptionId,
        DateTime startedAt,
        DateTime expiresAt)
    {
        var session = new SubscriptionSession
        {
            SessionId = sessionId,
            Status = SubscriptionSessionStatus.Completed,
        };

        session.Put(new SubscriptionsMetadata
        {
            Subscriptions =
            [
                new SubscriptionInfo
                {
                    SubscriptionId = subscriptionId,
                    StartedAt = startedAt,
                    ExpiresAt = expiresAt,
                    Gateway = "stripe",
                    LineItems =
                    [
                        new InvoiceLineItem
                        {
                            ItemId = "line-1",
                            Quantity = 1,
                            UnitPrice = 30.00m,
                            Subscription = new SubscriptionPlan
                            {
                                BillingDuration = 1,
                                DurationType = DurationType.Month,
                            },
                        },
                    ],
                },
            ],
        });

        return session;
    }

    private static SubscriptionPaymentHandler CreateHandler(
        SubscriptionSession session,
        out Mock<ISubscriptionSessionStore> sessionStore,
        TestClock clock)
    {
        sessionStore = new Mock<ISubscriptionSessionStore>();
        sessionStore.Setup(s => s.GetAsync(session.SessionId)).ReturnsAsync(session);

        return new SubscriptionPaymentHandler(
            PaymentTestHelpers.CreatePaymentSession(),
            new Mock<IStripePaymentIntentService>().Object,
            sessionStore.Object,
            new NullSubscriptionTaxService(),
            clock);
    }

    private static PaymentSucceededContext CreateContext(string sessionId, string subscriptionId, string transactionId, decimal amount)
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
