using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// How the queue-treatment sweep is scoped.
/// </summary>
/// <remarks>
/// Sending a caller to voicemail stops their hold music immediately but registers the greeting as a provider
/// command whose dispatch is scheduled for after the shell scope commits — the command row has to be committed
/// before another scope can pick it up. When the whole run shared one scope, "after commit" meant "after the last
/// sweep", so the caller heard the music stop and then dead air until the run ended: twenty seconds on one live
/// call and forty on another. A scope per sweep is what keeps that delay down to the sweep interval.
/// </remarks>
public sealed class QueueTreatmentBackgroundTaskTests
{
    [Fact]
    public async Task EachSweep_ReachesAScopeBoundary_RatherThanHoldingDeferredWorkUntilTheRunEnds()
    {
        // Arrange
        var scopesOpened = 0;
        var sweeps = 0;

        // The run loops until its fifty-second budget expires, so the sweep itself ends the run once it has
        // enough to measure. Counting from the sweep rather than from the scope means a run that never opens a
        // scope still terminates, and fails on the assertion rather than on the clock.
        using var cancellation = new CancellationTokenSource();

        void OnSweep()
        {
            if (++sweeps >= 2)
            {
                cancellation.Cancel();
            }
        }

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IServiceProvider, Task>>()))
            .Returns<Func<IServiceProvider, Task>>(async operation =>
            {
                scopesOpened++;

                await using var scoped = BuildSweepServices(OnSweep);
                await operation(scoped);
            });

        // The run's own provider can serve a sweep too, so a run that swept without opening a scope would
        // succeed at sweeping and fail only on the count. That is the distinction under test.
        await using var serviceProvider = BuildRunServices(scopeExecutor.Object, OnSweep);

        // Act
        await new QueueTreatmentBackgroundTask().DoWorkAsync(serviceProvider, cancellation.Token);

        // Assert
        Assert.True(sweeps >= 2, $"The run swept {sweeps} time(s); the test needs at least two to compare.");
        Assert.Equal(sweeps, scopesOpened);
    }

    [Fact]
    public async Task ASweepThatCannotClaimTheFeature_DoesNoWorkAtAll()
    {
        // Arrange
        // Another node holds the lease. Opening scopes and sweeping anyway would double-treat every caller.
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);

        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(manager => manager.TryEnter(It.IsAny<string>()))
            .Returns((IContactCenterFeatureWorkLease)null);

        var services = new ServiceCollection();
        services.AddSingleton(workManager.Object);
        services.AddSingleton(scopeExecutor.Object);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();

        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        await new QueueTreatmentBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        // A strict mock fails the test if the run opened a scope despite losing the lease.
        scopeExecutor.Verify(
            executor => executor.ExecuteAsync(It.IsAny<Func<IServiceProvider, Task>>()),
            Times.Never);
    }

    private static ServiceProvider BuildRunServices(IContactCenterScopeExecutor scopeExecutor, Action onSweep)
    {
        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(manager => manager.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var services = BuildSweepServiceCollection(onSweep);
        services.AddSingleton(workManager.Object);
        services.AddSingleton(scopeExecutor);
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildSweepServices(Action onSweep)
        => BuildSweepServiceCollection(onSweep).BuildServiceProvider();

    private static ServiceCollection BuildSweepServiceCollection(Action onSweep)
    {
        var queue = new ActivityQueue { ItemId = "queue-1" };

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                onSweep();

                return [queue];
            });

        var services = new ServiceCollection();
        services.AddSingleton(queueManager.Object);
        services.AddSingleton(new Mock<IQueueTreatmentService>().Object);
        services.AddSingleton(new Mock<IActivityQueueService>().Object);
        services.AddSingleton(new Mock<IQueueLimitService>().Object);

        return services;
    }
}
