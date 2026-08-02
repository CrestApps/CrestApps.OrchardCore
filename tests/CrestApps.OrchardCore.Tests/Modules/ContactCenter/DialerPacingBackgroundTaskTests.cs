using System.Reflection;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerPacingBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_RunsEveryEnabledProfileWithinBudget()
    {
        // Arrange
        var profile1 = new DialerProfile { Name = "profile-1" };
        var profile2 = new DialerProfile { Name = "profile-2" };
        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile1, profile2]);
        var dialerService = new Mock<IDialerService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, dialerService, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        dialerService.Verify(
            service => service.RunCycleAsync(profile1, It.IsAny<CancellationToken>()),
            Times.Once);
        dialerService.Verify(
            service => service.RunCycleAsync(profile2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenFeatureIsQuiescing_DoesNoWork()
    {
        // Arrange
        var dialerManager = new Mock<IDialerProfileManager>();
        var dialerService = new Mock<IDialerService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        var workManager = new TestContactCenterFeatureWorkManager();
        workManager.Quiesce(ContactCenterConstants.Feature.DialerAutomated);

        await using var serviceProvider = CreateServiceProvider(dialerManager, dialerService, clock, workManager);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        dialerManager.Verify(
            manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        dialerService.Verify(
            service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenRunExceedsItsTimeBudget_DefersRemainingProfilesToTheNextTick()
    {
        // Arrange
        // The run is bounded to a wall-clock budget below the lock expiration. Advance the clock past the
        // budget while pacing the first profile and assert the second profile is not paced, proving a run
        // cannot grow without bound (and therefore cannot outlive its lock and self-overlap on another node).
        var current = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => current);

        var profile1 = new DialerProfile { Name = "profile-1" };
        var profile2 = new DialerProfile { Name = "profile-2" };
        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile1, profile2]);
        var dialerService = new Mock<IDialerService>();
        dialerService
            .Setup(service => service.RunCycleAsync(profile1, It.IsAny<CancellationToken>()))
            .Callback(() => current = current.AddMilliseconds(100_000))
            .ReturnsAsync(0);

        await using var serviceProvider = CreateServiceProvider(dialerManager, dialerService, clock);

        // Act
        await new DialerPacingBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        dialerService.Verify(
            service => service.RunCycleAsync(profile1, It.IsAny<CancellationToken>()),
            Times.Once);
        dialerService.Verify(
            service => service.RunCycleAsync(profile2, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DoWorkAsync_WhenShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var profile = new DialerProfile { Name = "profile-1" };
        var dialerManager = new Mock<IDialerProfileManager>();
        dialerManager
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([profile]);
        var dialerService = new Mock<IDialerService>();
        dialerService
            .Setup(service => service.RunCycleAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc));

        await using var serviceProvider = CreateServiceProvider(dialerManager, dialerService, clock);

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

    private static ServiceProvider CreateServiceProvider(
        Mock<IDialerProfileManager> dialerManager,
        Mock<IDialerService> dialerService,
        Mock<IClock> clock,
        IContactCenterFeatureWorkManager workManager = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(dialerManager.Object);
        services.AddSingleton(dialerService.Object);
        services.AddSingleton(clock.Object);
        services.AddSingleton(workManager ?? new TestContactCenterFeatureWorkManager());
        services.AddLogging();

        return services.BuildServiceProvider();
    }
}
