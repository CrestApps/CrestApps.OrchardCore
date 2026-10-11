using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The lifecycle service is the only place a subscription changes state, and its callers genuinely race: a
/// gateway webhook, a nightly sweep, an operator, and the customer can all arrive at once. These tests pin
/// the outcomes that cost money if they go wrong: never billing a cycle twice, never taking away access a
/// customer paid for, and never reviving an agreement somebody canceled.
/// </summary>
public sealed class DefaultSubscriptionLifecycleServiceTests
{
    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordRenewalAsync_AdvancesThePeriodAndSchedulesTheNextCycle()
    {
        // Arrange
        var (service, store) = CreateService(CreateSubscription());
        var periodStart = _now;

        // Act
        var subscription = await service.RecordRenewalAsync("sub-1", periodStart, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(2, subscription.CyclesBilled);
        Assert.Equal(periodStart, subscription.CurrentPeriodStartUtc);
        Assert.Equal(periodStart.AddMonths(1), subscription.CurrentPeriodEndUtc);
        Assert.Equal(periodStart.AddMonths(1), subscription.NextBillingUtc);
        Assert.Contains(subscription.Events, e => e.Type == SubscriptionEventType.Renewed);
        Assert.Single(store.Subscriptions);
    }

    /// <summary>
    /// A webhook and the renewal sweep both report the same cycle. Applying it twice would move the
    /// customer a whole period ahead and skip a payment.
    /// </summary>
    [Fact]
    public async Task RecordRenewalAsync_ForTheSameCycleTwice_AdvancesOnce()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());
        var periodStart = _now;

        // Act
        await service.RecordRenewalAsync("sub-1", periodStart, cancellationToken: TestContext.Current.CancellationToken);
        var subscription = await service.RecordRenewalAsync("sub-1", periodStart, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, subscription.CyclesBilled);
        Assert.Equal(periodStart.AddMonths(1), subscription.CurrentPeriodEndUtc);
    }

    /// <summary>
    /// An agreement sold for a fixed number of cycles must stop scheduling a next bill once it has billed
    /// them all, even though this cycle succeeded.
    /// </summary>
    [Fact]
    public async Task RecordRenewalAsync_WhenTheAgreedCyclesAreReached_StopsScheduling()
    {
        // Arrange
        var subscription = CreateSubscription();

        subscription.BillingCycleLimit = 2;

        var (service, _) = CreateService(subscription);

        // Act
        var updated = await service.RecordRenewalAsync("sub-1", _now, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, updated.CyclesBilled);
        Assert.Null(updated.NextBillingUtc);
    }

    [Fact]
    public async Task MarkPastDueAsync_StartsTheGraceWindow()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        // Act
        var subscription = await service.MarkPastDueAsync("sub-1", "The card was declined.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(_now, subscription.PastDueSinceUtc);
        Assert.Equal(_now.AddDays(7), subscription.GraceEndsUtc);
        Assert.True(subscription.IsCurrent(_now));
    }

    /// <summary>
    /// A second failure inside the same window must not restart the grace clock, or a customer whose card
    /// keeps failing would keep access indefinitely.
    /// </summary>
    [Fact]
    public async Task MarkPastDueAsync_Twice_DoesNotRestartTheGraceWindow()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        await service.MarkPastDueAsync("sub-1", "First failure.", TestContext.Current.CancellationToken);

        // Act
        var subscription = await service.MarkPastDueAsync("sub-1", "Second failure.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddDays(7), subscription.GraceEndsUtc);
        Assert.Single(subscription.Events, e => e.Type == SubscriptionEventType.PaymentFailed);
    }

    /// <summary>
    /// A customer who cancels mid-cycle keeps the access they already paid for. Ending it immediately would
    /// take away what they bought.
    /// </summary>
    [Fact]
    public async Task CancelAsync_AtPeriodEnd_KeepsAccessUntilThePaidPeriodEnds()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        // Act
        var subscription = await service.CancelAsync("sub-1", atPeriodEnd: true, "The customer canceled.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.Null(subscription.NextBillingUtc);
        Assert.Equal(_now.AddDays(20), subscription.CurrentPeriodEndUtc);
        Assert.True(subscription.IsCurrent(_now));
        Assert.False(subscription.IsCurrent(_now.AddDays(21)));
    }

    [Fact]
    public async Task CancelAsync_Immediately_EndsAccessNow()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        // Act
        var subscription = await service.CancelAsync("sub-1", atPeriodEnd: false, "Fraud.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, subscription.CurrentPeriodEndUtc);
        Assert.False(subscription.IsCurrent(_now.AddSeconds(1)));
    }

    /// <summary>
    /// A provider status arriving after a local cancellation must not restart billing for a customer who
    /// already left.
    /// </summary>
    [Fact]
    public async Task SyncFromProviderAsync_DoesNotReviveACanceledSubscription()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        await service.CancelAsync("sub-1", atPeriodEnd: true, "The customer canceled.", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var subscription = await service.SyncFromProviderAsync("sub-1", new SubscriptionProviderSyncContext
        {
            Status = SubscriptionStatus.Active,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
    }

    /// <summary>
    /// A gateway that suspends collection still reports the agreement as active, so an unrelated change
    /// arriving afterwards must not be read as "the provider resumed it".
    /// </summary>
    /// <remarks>
    /// Live testing found this the hard way: suspending an agreement produced a provider notification, the
    /// notification synchronized the status back to active, and billing quietly resumed while the admin said
    /// it had been suspended.
    /// </remarks>
    [Fact]
    public async Task SyncFromProviderAsync_DoesNotResumeAPausedSubscription()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        await service.PauseAsync("sub-1", "Suspended by an administrator.", TestContext.Current.CancellationToken);

        // Act
        var subscription = await service.SyncFromProviderAsync("sub-1", new SubscriptionProviderSyncContext
        {
            Status = SubscriptionStatus.Active,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Paused, subscription.Status);
    }

    /// <summary>
    /// A provider that really did end the agreement still wins over a local suspension: there is nothing
    /// left to resume.
    /// </summary>
    [Fact]
    public async Task SyncFromProviderAsync_LetsTheProviderEndAPausedSubscription()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        await service.PauseAsync("sub-1", "Suspended by an administrator.", TestContext.Current.CancellationToken);

        // Act
        var subscription = await service.SyncFromProviderAsync("sub-1", new SubscriptionProviderSyncContext
        {
            Status = SubscriptionStatus.Canceled,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
    }

    /// <summary>
    /// A synchronization names the gateway it came from, which is what stops a handler from sending the
    /// change straight back and undoing what the gateway just reported.
    /// </summary>
    [Fact]
    public async Task SyncFromProviderAsync_TellsHandlersWhichGatewayReportedIt()
    {
        // Arrange
        var recorder = new RecordingLifecycleHandler();
        var (service, _) = CreateService(CreateSubscription(), recorder);

        // Act
        await service.SyncFromProviderAsync("sub-1", new SubscriptionProviderSyncContext
        {
            Status = SubscriptionStatus.PastDue,
            Source = "Stripe",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Stripe", Assert.Single(recorder.Contexts).Source);
    }

    [Fact]
    public async Task SyncFromProviderAsync_AppliesThePeriodTheProviderReports()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());
        var providerPeriodEnd = _now.AddDays(45);

        // Act
        var subscription = await service.SyncFromProviderAsync("sub-1", new SubscriptionProviderSyncContext
        {
            CurrentPeriodEndUtc = providerPeriodEnd,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(providerPeriodEnd, subscription.CurrentPeriodEndUtc);
        Assert.Equal(providerPeriodEnd, subscription.NextBillingUtc);
    }

    [Fact]
    public async Task ExpireAsync_EndsTheAgreementAndStopsBilling()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        // Act
        var subscription = await service.ExpireAsync("sub-1", "The grace period elapsed.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
        Assert.Null(subscription.NextBillingUtc);
        Assert.False(subscription.IsCurrent(_now));
    }

    [Fact]
    public async Task ResumeAsync_ReturnsAPastDueSubscriptionToActive()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        await service.MarkPastDueAsync("sub-1", "The card was declined.", TestContext.Current.CancellationToken);

        // Act
        var subscription = await service.ResumeAsync("sub-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.PastDueSinceUtc);
        Assert.Null(subscription.GraceEndsUtc);
    }

    [Fact]
    public async Task MutatingAnUnknownSubscription_ReturnsNull()
    {
        // Arrange
        var (service, _) = CreateService(CreateSubscription());

        // Act & Assert
        Assert.Null(await service.CancelAsync("missing", atPeriodEnd: true, "n/a", cancellationToken: TestContext.Current.CancellationToken));
    }

    private static Subscription CreateSubscription()
        => new()
        {
            ItemId = "sub-1",
            Title = "Membership",
            OwnerId = "owner-1",
            ProviderKey = "Stripe",
            ProviderSubscriptionId = "sub_stripe_1",
            Status = SubscriptionStatus.Active,
            Currency = "USD",
            Amount = 20m,
            TaxAmount = 2m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CyclesBilled = 1,
            CurrentPeriodStartUtc = _now.AddDays(-10),
            CurrentPeriodEndUtc = _now.AddDays(20),
            NextBillingUtc = _now.AddDays(20),
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

    private static (DefaultSubscriptionLifecycleService Service, InMemorySubscriptionStore Store) CreateService(
        Subscription seed,
        ISubscriptionLifecycleHandler handler = null)
    {
        var store = new InMemorySubscriptionStore(seed);

        var manager = new SubscriptionManager(
            store,
            [],
            NullLogger<CatalogManager<Subscription>>.Instance);

        var service = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            new TestClock(_now),
            handler is null ? [] : [handler],
            NullLogger<DefaultSubscriptionLifecycleService>.Instance);

        return (service, store);
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
