using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// How the queue-treatment sweep runs.
/// </summary>
/// <remarks>
/// Orchard runs a tenant's background tasks one after another. This task used to sweep every ten seconds for fifty
/// seconds of each minute to time announcements and overflow, and every task queued behind it waited that long.
/// Announcements are now timed by each queue's in-process treatment deadline, so a run makes one pass, in one scope,
/// and hands the loop back. The pass still has a scope of its own: sending a caller to voicemail registers a provider
/// command whose dispatch is scheduled for after the scope commits.
/// </remarks>
public sealed class QueueTreatmentBackgroundTaskTests
{
    [Fact]
    public async Task ARun_MakesOnePassInItsOwnScope_AndReturnsTheLoopAtOnce()
    {
        // Arrange
        var scopesOpened = 0;
        var sweeps = 0;
        var enforcer = new Mock<IQueueTreatmentDeadlineEnforcer>();

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IServiceProvider, Task>>()))
            .Returns<Func<IServiceProvider, Task>>(async operation =>
            {
                scopesOpened++;

                await using var scoped = BuildSweepServiceCollection(() => sweeps++, enforcer.Object).BuildServiceProvider();
                await operation(scoped);
            });

        // The run's own provider can serve a sweep too, so a run that swept without opening a scope would succeed at
        // sweeping and fail only on the count.
        await using var serviceProvider = BuildRunServices(scopeExecutor.Object, () => sweeps++, enforcer.Object);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // Act
        var run = new QueueTreatmentBackgroundTask().DoWorkAsync(serviceProvider, cancellation.Token);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == run;

        // A run that is still looping is stopped here, so a failure does not leave it sweeping behind the next test.
        await cancellation.CancelAsync();
        await run;

        // Assert
        Assert.True(finished, "The run held the background loop instead of making one pass.");
        Assert.Equal(1, sweeps);
        Assert.Equal(1, scopesOpened);
    }

    [Fact]
    public async Task APass_PlaysWhatIsDueAndArmsTheNextDeadline_ForEveryQueue()
    {
        // Arrange
        var enforcer = new Mock<IQueueTreatmentDeadlineEnforcer>();
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IServiceProvider, Task>>()))
            .Returns<Func<IServiceProvider, Task>>(async operation =>
            {
                await using var scoped = BuildSweepServiceCollection(() => { }, enforcer.Object, "queue-1", "queue-2").BuildServiceProvider();
                await operation(scoped);
            });

        await using var serviceProvider = BuildRunServices(scopeExecutor.Object, () => { }, enforcer.Object);

        // Act
        await new QueueTreatmentBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        enforcer.Verify(value => value.RunAndArmAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
        enforcer.Verify(value => value.RunAndArmAsync("queue-2", It.IsAny<CancellationToken>()), Times.Once);
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

    private static ServiceProvider BuildRunServices(
        IContactCenterScopeExecutor scopeExecutor,
        Action onSweep,
        IQueueTreatmentDeadlineEnforcer enforcer)
    {
        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(manager => manager.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var services = BuildSweepServiceCollection(onSweep, enforcer);
        services.AddSingleton(workManager.Object);
        services.AddSingleton(scopeExecutor);
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private static ServiceCollection BuildSweepServiceCollection(
        Action onSweep,
        IQueueTreatmentDeadlineEnforcer enforcer,
        params string[] queueIds)
    {
        var queues = (queueIds.Length == 0 ? ["queue-1"] : queueIds)
            .Select(queueId => new ActivityQueue { ItemId = queueId })
            .ToArray();

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                onSweep();

                return [.. queues];
            });

        var services = new ServiceCollection();
        services.AddSingleton(queueManager.Object);
        services.AddSingleton(enforcer);
        services.AddSingleton(new Mock<IQueueTreatmentService>().Object);
        services.AddSingleton(new Mock<IActivityQueueService>().Object);
        services.AddSingleton(new Mock<IQueueLimitService>().Object);

        return services;
    }
}
