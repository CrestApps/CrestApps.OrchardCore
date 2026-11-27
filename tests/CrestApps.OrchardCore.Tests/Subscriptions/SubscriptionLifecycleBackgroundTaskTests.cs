using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.Tasks;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The sweep is the only thing that moves an agreement whose provider has no gateway. Before it did, a Pay
/// Later subscription sat at its first period forever while the customer kept being invoiced.
/// </summary>
public sealed class SubscriptionLifecycleBackgroundTaskTests
{
    private static readonly DateTime _now = new(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// An offline agreement whose next billing date has passed is advanced here, because nobody else will.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_AdvancesADueAgreementWhoseProviderHasNoGateway()
    {
        var subscription = CreateDue("PayLater");
        var (task, services, store) = CreateTask(subscription, isGateway: false);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        var advanced = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Active, advanced.Status);
        Assert.Equal(2, advanced.CyclesBilled);
        Assert.Equal(_now.AddDays(-1), advanced.CurrentPeriodStartUtc);
        Assert.Equal(_now.AddDays(-1).AddMonths(1), advanced.CurrentPeriodEndUtc);
        Assert.Equal(_now.AddDays(-1).AddMonths(1), advanced.NextBillingUtc);
    }

    /// <summary>
    /// A gateway reports its own cycles through its webhook. Advancing one here would record a payment the
    /// gateway never confirmed.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_LeavesADueGatewayAgreementToItsWebhook()
    {
        var subscription = CreateDue("Stripe");
        var (task, services, store) = CreateTask(subscription, isGateway: true);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        var untouched = Assert.Single(store.Subscriptions);

        Assert.Equal(1, untouched.CyclesBilled);
        Assert.Equal(_now.AddDays(-1), untouched.NextBillingUtc);
    }

    /// <summary>
    /// The end of a trial is the first cycle to bill. Skipping it would give the plan away for free forever.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_ConvertsAnExpiredOfflineTrialIntoItsFirstBilledCycle()
    {
        var subscription = CreateDue("PayLater");
        subscription.Status = SubscriptionStatus.Trialing;
        subscription.CyclesBilled = 0;

        var (task, services, store) = CreateTask(subscription, isGateway: false);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        var converted = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Active, converted.Status);
        Assert.Equal(1, converted.CyclesBilled);
    }

    /// <summary>
    /// M9 (final security review): a subscription canceled at period end kept what it granted, a sold site included, once
    /// the period ended, because no sweep ever looked at it again.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_ExpiresACanceledSubscriptionWhosePaidPeriodEnded()
    {
        var subscription = CreateDue("Stripe");
        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CancelAtPeriodEnd = true;
        subscription.NextBillingUtc = null;
        subscription.CurrentPeriodEndUtc = _now.AddHours(-1);

        var (task, services, store) = CreateTask(subscription, isGateway: true);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        Assert.Equal(SubscriptionStatus.Expired, Assert.Single(store.Subscriptions).Status);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): the first sweep after the upgrade found every canceled subscription ever
    /// recorded and raised the expired workflow event for each, so a store with an "expired" e-mail mailed everyone who
    /// ever canceled. A subscription whose paid period ended long before the sweep found it is expired without the event;
    /// one that just lapsed still raises it.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_ExpiresTheBacklogOfLongCanceledSubscriptions_WithoutRaisingTheirEvent()
    {
        var workflows = new Mock<IWorkflowManager>();
        var (task, services, store) = CreateTask(
            [Canceled("canceled-last-year", _now.AddYears(-1)), Canceled("canceled-last-month", _now.AddMonths(-1)), Canceled("just-lapsed", _now.AddHours(-1))],
            isGateway: true,
            handlers: [new WorkflowSubscriptionLifecycleHandler(workflows.Object, NullLogger<WorkflowSubscriptionLifecycleHandler>.Instance)]);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        Assert.All(store.Subscriptions, subscription => Assert.Equal(SubscriptionStatus.Expired, subscription.Status));
        workflows.Verify(w => w.TriggerEventAsync(SubscriptionExpiredEvent.EventName, It.IsAny<IDictionary<string, object>>(), "just-lapsed", It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
        workflows.Verify(w => w.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), "canceled-last-year", It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        workflows.Verify(w => w.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), "canceled-last-month", It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): the lapsed agreements were read in one unpaged query. They are read a page at
    /// a time, and a backlog larger than a page is still expired in the same run.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_ExpiresABacklogLargerThanOnePage()
    {
        var seeds = Enumerable.Range(1, 250).Select(index => Canceled($"canceled-{index}", _now.AddYears(-1))).ToArray();
        var (task, services, store) = CreateTask(seeds, isGateway: true, handlers: []);

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        Assert.Equal(250, store.Subscriptions.Count(subscription => subscription.Status == SubscriptionStatus.Expired));
    }

    private static Subscription Canceled(string itemId, DateTime periodEnd)
    {
        var subscription = CreateDue("Stripe");
        subscription.ItemId = itemId;
        subscription.Status = SubscriptionStatus.Canceled;
        subscription.CancelAtPeriodEnd = true;
        subscription.NextBillingUtc = null;
        subscription.CurrentPeriodEndUtc = periodEnd;

        return subscription;
    }

    private static Subscription CreateDue(string providerKey)
        => new()
        {
            ItemId = "subscription-1",
            Title = "Membership",
            OwnerId = "owner-1",
            ProviderKey = providerKey,
            ProviderSubscriptionId = "agreement-1",
            Status = SubscriptionStatus.Active,
            Currency = "USD",
            Amount = 20m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CyclesBilled = 1,
            CurrentPeriodStartUtc = _now.AddDays(-1).AddMonths(-1),
            CurrentPeriodEndUtc = _now.AddDays(-1),
            NextBillingUtc = _now.AddDays(-1),
            CreatedUtc = _now.AddMonths(-1),
            UpdatedUtc = _now.AddMonths(-1),
        };

    private static (SubscriptionLifecycleBackgroundTask Task, IServiceProvider Services, InMemorySubscriptionStore Store) CreateTask(Subscription seed, bool isGateway)
        => CreateTask([seed], isGateway, handlers: []);

    private static (SubscriptionLifecycleBackgroundTask Task, IServiceProvider Services, InMemorySubscriptionStore Store) CreateTask(
        Subscription[] seeds,
        bool isGateway,
        ISubscriptionLifecycleHandler[] handlers)
    {
        var seed = seeds[0];
        var store = new InMemorySubscriptionStore(seeds);
        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);

        var lifecycle = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            new TestClock(_now),
            handlers,
            NullLogger<DefaultSubscriptionLifecycleService>.Instance,
            new FakePaymentProviderResolver("PayLater"));

        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(p => p.Key).Returns(seed.ProviderKey);
        provider.SetupGet(p => p.Capabilities).Returns(new PaymentProviderCapabilities
        {
            SupportsOneTimePayments = true,
            SupportsRecurringPayments = true,
            SupportsEmbeddedElements = isGateway,
        });

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(r => r.GetProvider(seed.ProviderKey)).Returns(provider.Object);

        var services = new ServiceCollection();
        services.AddSingleton<ISubscriptionManager>(manager);
        services.AddSingleton<ISubscriptionLifecycleService>(lifecycle);
        services.AddSingleton(resolver.Object);
        services.AddSingleton<IClock>(new TestClock(_now));
        services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));

        var task = new SubscriptionLifecycleBackgroundTask(NullLogger<SubscriptionLifecycleBackgroundTask>.Instance);

        return (task, services.BuildServiceProvider(), store);
    }
}
