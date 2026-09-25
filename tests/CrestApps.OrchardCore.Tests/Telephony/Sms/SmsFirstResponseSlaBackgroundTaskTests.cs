using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// The SMS first-response task makes one pass and hands the tenant's background loop back, holding an in-process
/// deadline for the next pass instead of looping inside its run for thirty seconds.
/// </summary>
public sealed class SmsFirstResponseSlaBackgroundTaskTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ARun_MakesOnePass_AndReturnsTheLoopAtOnce()
    {
        // Arrange
        var (services, sla, scheduler, _) = CreateServices(nextDueUtc: null);
        await using var serviceProvider = services.BuildServiceProvider();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        var run = new SmsFirstResponseSlaBackgroundTask().DoWorkAsync(serviceProvider, cancellation.Token);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == run;

        await cancellation.CancelAsync();

        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
            // Only a run that was still looping is cancelled here.
        }

        // Assert
        Assert.True(finished, "The run held the background loop instead of making one pass.");
        sla.Verify(value => value.EscalateOverdueAsync(It.IsAny<CancellationToken>()), Times.Once);
        scheduler.Verify(
            value => value.Schedule(SmsFirstResponseSlaSweep.DeadlineKey, _now + SmsFirstResponseSlaSweep.MaximumInterval, It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task APass_HoldsTheNextPassForTheSoonestTargetStillAhead()
    {
        // Arrange
        var (services, _, _, session) = CreateServices(nextDueUtc: _now.AddSeconds(12));
        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        var next = await serviceProvider.GetRequiredService<SmsFirstResponseSlaSweep>().RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddSeconds(12), next);
        session.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APass_LooksAgainWithinTheOldCadence_WhenTheSoonestTargetIsFurtherOut()
    {
        // Arrange
        // A thread placed after this pass may carry a shorter target than any on record now.
        var (services, _, _, _) = CreateServices(nextDueUtc: _now.AddMinutes(5));
        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        var next = await serviceProvider.GetRequiredService<SmsFirstResponseSlaSweep>().RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now + SmsFirstResponseSlaSweep.MaximumInterval, next);
    }

    [Fact]
    public async Task APass_AnnouncesNothing_WhileAnotherPassHoldsTheLock()
    {
        // Arrange
        var (services, sla, _, _) = CreateServices(nextDueUtc: null, lockAcquired: false);
        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        var next = await serviceProvider.GetRequiredService<SmsFirstResponseSlaSweep>().RunAsync(TestContext.Current.CancellationToken);

        // Assert
        sla.Verify(value => value.EscalateOverdueAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(_now + SmsFirstResponseSlaSweep.LockedRetryInterval, next);
    }

    private static (ServiceCollection Services, Mock<ISmsFirstResponseSlaService> Sla, Mock<IContactCenterDeadlineScheduler> Scheduler, Mock<ISession> Session) CreateServices(
        DateTime? nextDueUtc,
        bool lockAcquired = true)
    {
        var sla = new Mock<ISmsFirstResponseSlaService>();
        var store = new Mock<ISmsConversationStore>();
        store
            .Setup(value => value.GetNextFirstResponseDueUtcAsync(_now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextDueUtc);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => lockAcquired ? (new Mock<ILocker>().Object, true) : (null, false));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var scheduler = new Mock<IContactCenterDeadlineScheduler>();
        var session = new Mock<ISession>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(sla.Object);
        services.AddSingleton(store.Object);
        services.AddSingleton(scheduler.Object);
        services.AddSingleton(distributedLock.Object);
        services.AddSingleton(session.Object);
        services.AddSingleton(clock.Object);
        services.AddScoped<SmsFirstResponseSlaSweep>();

        return (services, sla, scheduler, session);
    }
}
