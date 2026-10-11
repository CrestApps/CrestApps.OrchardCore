using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
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
    {
        var store = new InMemorySubscriptionStore(seed);
        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);

        var lifecycle = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            new TestClock(_now),
            [],
            NullLogger<DefaultSubscriptionLifecycleService>.Instance);

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
