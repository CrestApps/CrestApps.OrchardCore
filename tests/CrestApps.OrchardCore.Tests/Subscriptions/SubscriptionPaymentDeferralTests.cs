using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Locking;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C5: a subscription bought with a promise to pay (Pay Later, a bank transfer) granted its roles at once, and with
/// no due date its transaction never became overdue, so it never lapsed either. It now waits, incomplete and granting
/// nothing, until its first transaction is paid.
/// </summary>
public sealed class SubscriptionPaymentDeferralTests
{
    private const string PayLater = "pay-later";

    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string _planObligation = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1));

    [Fact]
    public async Task ASubscriptionPaidLater_GrantsNothing_UntilItsFirstTransactionIsPaid()
    {
        // Arrange
        var store = new InMemorySubscriptionStore();
        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);
        var entitlements = new RecordingLifecycleHandler();
        var activation = CreateActivationHandler(manager, entitlements, defers: true);

        // Act: the checkout completes on a promise to pay.
        await activation.CompletedAsync(CreateContext());

        // Assert: the agreement exists, but nothing it grants is current yet.
        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Incomplete, subscription.Status);
        Assert.False(subscription.IsCurrent(_now));
        Assert.All(entitlements.Contexts, context => Assert.False(context.Subscription.IsCurrent(_now)));

        // Act: the transaction the checkout left is paid.
        var lifecycle = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            new TestClock(_now),
            [entitlements],
            NullLogger<DefaultSubscriptionLifecycleService>.Instance);
        var paid = new SubscriptionFirstPaymentTransactionHandler(lifecycle, manager, new CrestApps.OrchardCore.Tests.Transactions.FakeTransactionStore());

        // SEC-C5: Pay Later records one transaction per obligation. Paying the checkout's one-time balance (a sign-up fee,
        // a small item next to the plan) is not paying for the plan's first cycle.
        await paid.StatusChangedAsync(
            new Transaction { CheckoutSessionId = "session-1", ObligationId = CheckoutObligations.OneTime, ReferenceType = "Subscription", ReferenceId = "plan-1" },
            TransactionStatus.Outstanding,
            TransactionStatus.Paid,
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriptionStatus.Incomplete, Assert.Single(store.Subscriptions).Status);

        // A later cycle of the same agreement does not start it either: only its own obligation does.
        await paid.StatusChangedAsync(
            new Transaction { CheckoutSessionId = "session-1", ObligationId = _planObligation + ":cycle-2", ReferenceType = "Subscription", ReferenceId = "plan-1" },
            TransactionStatus.Outstanding,
            TransactionStatus.Paid,
            TestContext.Current.CancellationToken);

        Assert.Equal(SubscriptionStatus.Incomplete, Assert.Single(store.Subscriptions).Status);

        await paid.StatusChangedAsync(
            new Transaction { CheckoutSessionId = "session-1", ObligationId = _planObligation, ReferenceType = "Subscription", ReferenceId = "plan-1" },
            TransactionStatus.Outstanding,
            TransactionStatus.Paid,
            TestContext.Current.CancellationToken);

        // Assert: it is active now, and the handlers were told so.
        subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(entitlements.Contexts[^1].Subscription.IsCurrent(_now));
        Assert.Equal(SubscriptionStatus.Incomplete, entitlements.Contexts[^1].PreviousStatus);
    }

    [Fact]
    public async Task ASubscriptionPaidByCard_IsActiveAtOnce()
    {
        // Arrange
        var store = new InMemorySubscriptionStore();
        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);
        var activation = CreateActivationHandler(manager, new RecordingLifecycleHandler(), defers: false);

        // Act
        await activation.CompletedAsync(CreateContext(gatewayId: "card"));

        // Assert
        Assert.Equal(SubscriptionStatus.Active, Assert.Single(store.Subscriptions).Status);
    }

    [Fact]
    public void IsDeferred_IsTrueOnlyForAPaymentFromAProviderThatDefersPayment()
    {
        var session = new CheckoutSession { SessionId = "session-1" };
        var resolver = CreateResolver(defers: true);

        Assert.False(CheckoutPaymentDeferral.IsDeferred(session, resolver));

        session.Put(new PaymentsMetadata { Payments = { ["p1"] = new PaymentRecord { GatewayId = "card" } } });

        Assert.False(CheckoutPaymentDeferral.IsDeferred(session, resolver));

        session.Put(new PaymentsMetadata { Payments = { ["p1"] = new PaymentRecord { GatewayId = "card" }, ["p2"] = new PaymentRecord { GatewayId = PayLater } } });

        Assert.True(CheckoutPaymentDeferral.IsDeferred(session, resolver));
    }

    private static SubscriptionActivationCheckoutHandler CreateActivationHandler(ISubscriptionManager manager, ISubscriptionLifecycleHandler handler, bool defers)
    {
        var attempt = new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = defers ? PayLater : "card",
            ObligationId = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1)),
            State = PaymentAttemptState.Succeeded,
            ProviderReference = "ref-1",
            Currency = "USD",
            ExpectedAmount = 20m,
        };

        return new SubscriptionActivationCheckoutHandler(
            new InMemoryPaymentAttemptStore(attempt),
            manager,
            Mock.Of<IContentManager>(),
            [handler],
            new TestClock(_now),
            NullLogger<SubscriptionActivationCheckoutHandler>.Instance,
            CreateResolver(defers));
    }

    private static ICheckoutPaymentProviderResolver CreateResolver(bool defers)
    {
        var deferring = new Mock<ICheckoutPaymentProvider>();
        deferring.Setup(provider => provider.Key).Returns(PayLater);
        deferring.Setup(provider => provider.Capabilities).Returns(new PaymentProviderCapabilities { DefersPayment = defers });

        var card = new Mock<ICheckoutPaymentProvider>();
        card.Setup(provider => provider.Key).Returns("card");
        card.Setup(provider => provider.Capabilities).Returns(new PaymentProviderCapabilities());

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(value => value.GetProvider(PayLater)).Returns(deferring.Object);
        resolver.Setup(value => value.GetProvider("card")).Returns(card.Object);

        return resolver.Object;
    }

    private static CheckoutFlowCompletedContext CreateContext(string gatewayId = PayLater)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = "owner-1",
            ReferenceType = "Subscription",
            ReferenceId = "plan-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Put(new PaymentsMetadata { Payments = { ["p1"] = new PaymentRecord { GatewayId = gatewayId } } });
        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            FirstRecurringPaymentAmount = 20m,
            DueNow = 20m,
            GrandTotal = 20m,
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "membership",
                    Description = "Membership",
                    Quantity = 1,
                    UnitPrice = 20m,
                    Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 },
                },
            ],
        });

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }

    private sealed class RecordingLifecycleHandler : SubscriptionLifecycleHandlerBase
    {
        public List<SubscriptionLifecycleContext> Contexts { get; } = [];

        public override Task ChangedAsync(SubscriptionLifecycleContext context)
        {
            Contexts.Add(context);

            return Task.CompletedTask;
        }
    }
}
