using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The ring-timeout backstop makes one pass and hands Orchard's background loop back.
/// </summary>
/// <remarks>
/// Orchard runs a tenant's background tasks one after another. This task used to tick inside its own run for most of
/// a minute, and every task queued behind it — the reservation expiry sweep included — waited that long: a
/// thirty-second offer rang for seventy-three. Ring windows are now enforced by in-process deadlines, so the sweep
/// only has to catch what those missed, once.
/// </remarks>
public sealed class DirectRingTimeoutBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_MakesOnePass_AndReturnsTheLoopAtOnce()
    {
        // Arrange
        var reservations = new Mock<IActivityReservationService>();
        var directHolds = new Mock<IDirectHoldTimeoutService>();

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IActivityReservationService, Task>>()))
            .Returns<Func<IActivityReservationService, Task>>(operation => operation(reservations.Object));
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<Func<IDirectHoldTimeoutService, Task>>()))
            .Returns<Func<IDirectHoldTimeoutService, Task>>(operation => operation(directHolds.Object));

        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager.Setup(manager => manager.TryEnter(It.IsAny<string>())).Returns(Mock.Of<IContactCenterFeatureWorkLease>());

        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IClock>(new TestClock())
            .AddSingleton(workManager.Object)
            .AddSingleton(scopeExecutor.Object)
            .AddSingleton(directHolds.Object)
            .BuildServiceProvider();

        // Act
        var run = new DirectRingTimeoutBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == run;

        // Assert
        Assert.True(finished, "The run held the background loop instead of making one pass.");
        reservations.Verify(service => service.ExpireDueAsync(It.IsAny<CancellationToken>()), Times.Once);
        directHolds.Verify(service => service.ProcessDueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
