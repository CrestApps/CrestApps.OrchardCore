using System.Reflection;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerPacingBackgroundTaskTests
{
    private static readonly string _queue1 = ContactCenterConstants.CampaignQueue.CreateId("camp-1");
    private static readonly string _queue2 = ContactCenterConstants.CampaignQueue.CreateId("camp-2");

    [Fact]
    public async Task DoWorkAsync_RequestsPacingOfEveryCampaignQueueWithWaitingWork()
    {
        // Arrange: two campaign queues each hold waiting inventory loaded under its own profile.
        var profile1 = new DialerProfile { ItemId = "dp1", Name = "profile-1", Mode = DialerMode.Power };
        var profile2 = new DialerProfile { ItemId = "dp2", Name = "profile-2", Mode = DialerMode.Progressive };

        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager.Setup(m => m.FindByIdAsync("dp1", It.IsAny<CancellationToken>())).ReturnsAsync(profile1);
        dialerManager.Setup(m => m.FindByIdAsync("dp2", It.IsAny<CancellationToken>())).ReturnsAsync(profile2);

        var queueItemStore = CreateQueueItemStore(
            (_queue1, "dp1"),
            (_queue2, "dp2"));

        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert: the minute task is the backstop; each queue is paced by its own run, under its own pacing lock.
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(_queue1), Times.Once);
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(_queue2), Times.Once);
        pacingScheduler.Verify(scheduler => scheduler.Request(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_SkipsQueuesWhoseWaitingWorkIsNotDialerInventory()
    {
        // Arrange: the campaign queue's head item carries no dialer profile (e.g. inbound-style work), so it is
        // not paced as outbound inventory.
        var dialerManager = new Mock<IDialerProfileManager>();
        var queueItemStore = CreateQueueItemStore((_queue1, null));
        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_IgnoresNonCampaignQueues()
    {
        // Arrange: a plain (inbound) queue with waiting work must not be paced by the outbound dialer.
        var dialerManager = new Mock<IDialerProfileManager>();
        var queueItemStore = CreateQueueItemStore(("inbound-queue", "dp1"));
        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenFeatureIsQuiescing_DoesNoWork()
    {
        // Arrange
        var dialerManager = new Mock<IDialerProfileManager>();
        var queueItemStore = new Mock<IQueueItemStore>();
        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        var workManager = new TestContactCenterFeatureWorkManager();
        workManager.Quiesce(ContactCenterConstants.Feature.DialerPaced);

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock, workManager);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        queueItemStore.Verify(
            store => store.GetWaitingQueueIdsAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenRunExceedsItsTimeBudget_DefersRemainingQueuesToTheNextTick()
    {
        // Arrange
        // The run is bounded to a wall-clock budget below the lock expiration. Advance the clock past the
        // budget while pacing the first queue and assert the second queue is not paced, proving a run cannot
        // grow without bound (and therefore cannot outlive its lock and self-overlap on another node).
        var current = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => current);

        var profile1 = new DialerProfile { ItemId = "dp1", Name = "profile-1", Mode = DialerMode.Power };
        var profile2 = new DialerProfile { ItemId = "dp2", Name = "profile-2", Mode = DialerMode.Progressive };

        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager.Setup(m => m.FindByIdAsync("dp1", It.IsAny<CancellationToken>())).ReturnsAsync(profile1);
        dialerManager.Setup(m => m.FindByIdAsync("dp2", It.IsAny<CancellationToken>())).ReturnsAsync(profile2);

        var queueItemStore = CreateQueueItemStore(
            (_queue1, "dp1"),
            (_queue2, "dp2"));

        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        pacingScheduler
            .Setup(scheduler => scheduler.RequestBackstop(_queue1))
            .Callback(() => current = current.AddMilliseconds(100_000));

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(_queue1), Times.Once);
        pacingScheduler.Verify(scheduler => scheduler.RequestBackstop(_queue2), Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var profile = new DialerProfile { ItemId = "dp1", Name = "profile-1", Mode = DialerMode.Power };
        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager.Setup(m => m.FindByIdAsync("dp1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var queueItemStore = CreateQueueItemStore((_queue1, "dp1"));

        queueItemStore
            .Setup(store => store.GetWaitingQueueIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var pacingScheduler = new Mock<IDialerPacingScheduler>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, pacingScheduler, queueItemStore, clock);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, cancellationSource.Token));
    }

    [Fact]
    public void BackgroundTaskMetadata_RunBudget_StaysBelowLockExpiration_WhichHasSafeMarginAboveSchedule()
    {
        // Arrange
        // Self-overlap across nodes is prevented by bounding each run to a wall-clock budget strictly below the
        // distributed-lock expiration, and by keeping that expiration above the schedule interval. Read both
        // private constants and assert the ordering: lock-expiration >= 2x schedule, lock-expiration > timeout,
        // and run-budget < lock-expiration.
        var attribute = typeof(DialerPacingBackgroundTask)
            .GetCustomAttributes(typeof(BackgroundTaskAttribute), inherit: false)
            .Cast<BackgroundTaskAttribute>()
            .Single();
        var lockExpirationMilliseconds = ReadPrivateConstant("LockExpirationMilliseconds");
        var maxRunDurationMilliseconds = ReadPrivateConstant("MaxRunDurationMilliseconds");

        // Act
        const int scheduleIntervalMilliseconds = 60_000;

        // Assert
        Assert.Equal("* * * * *", attribute.Schedule);
        Assert.Equal(lockExpirationMilliseconds, attribute.LockExpiration);
        Assert.True(
            attribute.LockExpiration >= 2 * scheduleIntervalMilliseconds,
            $"LockExpiration ({attribute.LockExpiration} ms) must be at least twice the {scheduleIntervalMilliseconds} ms schedule interval.");
        Assert.True(
            attribute.LockExpiration > attribute.LockTimeout,
            $"LockExpiration ({attribute.LockExpiration} ms) must exceed LockTimeout ({attribute.LockTimeout} ms).");
        Assert.True(
            maxRunDurationMilliseconds < lockExpirationMilliseconds,
            $"MaxRunDuration ({maxRunDurationMilliseconds} ms) must stay below LockExpiration ({lockExpirationMilliseconds} ms) so a run cannot outlive its lock.");
    }

    private static int ReadPrivateConstant(string name)
    {
        var field = typeof(DialerPacingBackgroundTask)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static);

        return (int)field.GetRawConstantValue();
    }

    private static Mock<IQueueItemStore> CreateQueueItemStore(params (string QueueId, string DialerProfileId)[] queues)
    {
        var store = new Mock<IQueueItemStore>();

        store
            .Setup(s => s.GetWaitingQueueIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queues.Select(queue => queue.QueueId).ToArray());

        foreach (var queue in queues)
        {
            store
                .Setup(s => s.FindNextWaitingAsync(queue.QueueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueItem { QueueId = queue.QueueId, ActivityItemId = "act", DialerProfileId = queue.DialerProfileId });
        }

        return store;
    }

    private static ServiceProvider CreateServiceProvider(
        Mock<IDialerProfileManager> dialerManager,
        Mock<IDialerPacingScheduler> pacingScheduler,
        Mock<IQueueItemStore> queueItemStore,
        Mock<IClock> clock,
        IContactCenterFeatureWorkManager workManager = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dialerManager.Object);
        services.AddSingleton(pacingScheduler.Object);
        services.AddSingleton(queueItemStore.Object);
        services.AddSingleton(clock.Object);
        services.AddSingleton(workManager ?? new TestContactCenterFeatureWorkManager());
        services.AddLogging();

        return services.BuildServiceProvider();
    }
}
