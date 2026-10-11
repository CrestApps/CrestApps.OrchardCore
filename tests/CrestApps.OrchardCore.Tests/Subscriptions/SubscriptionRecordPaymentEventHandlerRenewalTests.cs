using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// A gateway that bills a renewal reports it as a succeeded payment for a subscription cycle. That report is
/// the only thing that moves a gateway-owned agreement forward; before it was mapped, every Stripe agreement
/// stayed at its first period and lapsed from the site's point of view while the customer kept paying.
/// </summary>
public sealed class SubscriptionRecordPaymentEventHandlerRenewalTests
{
    private static readonly DateTime _now = new(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A cycle payment advances the agreement to the period the gateway named.
    /// </summary>
    [Fact]
    public async Task PaymentSucceededAsync_ForASubscriptionCycle_RecordsTheRenewal()
    {
        var (handler, store) = CreateHandler();

        await handler.PaymentSucceededAsync(new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_2",
            AmountPaid = 20m,
            Currency = "USD",
            GatewayId = "Stripe",
            Subscription = new SubscriptionPaymentInfo
            {
                SubscriptionId = "sub_1",
                PeriodStartUtc = _now,
                PeriodEndUtc = _now.AddMonths(1),
            },
        }, TestContext.Current.CancellationToken);

        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(2, subscription.CyclesBilled);
        Assert.Equal(_now, subscription.CurrentPeriodStartUtc);
        Assert.Equal(_now.AddMonths(1), subscription.CurrentPeriodEndUtc);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Contains(subscription.Events, e => e.Type == SubscriptionEventType.Renewed);
    }

    /// <summary>
    /// The first payment is what the checkout created the agreement from. Treating it as a renewal too would
    /// bill the customer two cycles for one payment.
    /// </summary>
    [Fact]
    public async Task PaymentSucceededAsync_ForTheFirstPayment_ChangesNothing()
    {
        var (handler, store) = CreateHandler();

        await handler.PaymentSucceededAsync(new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCreate,
            TransactionId = "in_1",
            AmountPaid = 20m,
            Subscription = new SubscriptionPaymentInfo { SubscriptionId = "sub_1", PeriodStartUtc = _now },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, Assert.Single(store.Subscriptions).CyclesBilled);
    }

    /// <summary>
    /// A webhook is delivered at least once. The second delivery of the same cycle must not advance twice.
    /// </summary>
    [Fact]
    public async Task PaymentSucceededAsync_DeliveredTwice_AdvancesOnce()
    {
        var (handler, store) = CreateHandler();

        var context = new PaymentSucceededContext
        {
            Reason = PaymentReason.SubscriptionCycle,
            TransactionId = "in_2",
            Subscription = new SubscriptionPaymentInfo { SubscriptionId = "sub_1", PeriodStartUtc = _now },
        };

        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);
        await handler.PaymentSucceededAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(store.Subscriptions).CyclesBilled);
    }

    private static (SubscriptionRecordPaymentEventHandler Handler, InMemorySubscriptionStore Store) CreateHandler()
    {
        var store = new InMemorySubscriptionStore(new Subscription
        {
            ItemId = "subscription-1",
            OwnerId = "owner-1",
            ProviderKey = "Stripe",
            ProviderSubscriptionId = "sub_1",
            Status = SubscriptionStatus.Active,
            Currency = "USD",
            Amount = 20m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CyclesBilled = 1,
            CurrentPeriodStartUtc = _now.AddMonths(-1),
            CurrentPeriodEndUtc = _now,
            NextBillingUtc = _now,
            CreatedUtc = _now.AddMonths(-1),
            UpdatedUtc = _now.AddMonths(-1),
        });

        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);

        var lifecycle = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            new TestClock(_now),
            [],
            NullLogger<DefaultSubscriptionLifecycleService>.Instance);

        var handler = new SubscriptionRecordPaymentEventHandler(
            manager,
            lifecycle,
            new TestClock(_now),
            NullLogger<SubscriptionRecordPaymentEventHandler>.Instance);

        return (handler, store);
    }
}
