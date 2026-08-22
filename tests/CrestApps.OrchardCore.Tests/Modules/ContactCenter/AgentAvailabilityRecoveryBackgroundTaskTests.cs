using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityRecoveryBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_InvokesTenantRecoveryServiceWithABudgetedToken()
    {
        // Arrange
        var capturedToken = CancellationToken.None;
        var recoveryService = new Mock<IAgentAvailabilityRecoveryService>();
        recoveryService
            .Setup(service => service.RecoverAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(token => capturedToken = token)
            .ReturnsAsync(2);
        var services = new ServiceCollection();
        services.AddSingleton(recoveryService.Object);
        services.AddSingleton(new Mock<ILogger<AgentAvailabilityRecoveryBackgroundTask>>().Object);
        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        await new AgentAvailabilityRecoveryBackgroundTask()
            .DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        recoveryService.Verify(
            service => service.RecoverAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // The recovery pass must run under the run-budget token (a linked token that cancels at the wall-clock
        // deadline), not the raw shutdown token, so a slow pass cannot outlive its distributed lock.
        Assert.True(capturedToken.CanBeCanceled);
        Assert.NotEqual(TestContext.Current.CancellationToken, capturedToken);
    }

    [Fact]
    public async Task DoWorkAsync_WhenTenantShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var recoveryService = new Mock<IAgentAvailabilityRecoveryService>();
        recoveryService
            .Setup(service => service.RecoverAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var services = new ServiceCollection();
        services.AddSingleton(recoveryService.Object);
        services.AddSingleton(new Mock<ILogger<AgentAvailabilityRecoveryBackgroundTask>>().Object);
        await using var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new AgentAvailabilityRecoveryBackgroundTask()
                .DoWorkAsync(serviceProvider, cancellationSource.Token));
    }

    [Fact]
    public void BackgroundTaskMetadata_RunBudget_StaysBelowLockExpiration_WhichHasSafeMarginAboveSchedule()
    {
        // Arrange
        // Self-overlap across nodes is prevented by bounding each run to a wall-clock budget strictly below the
        // distributed-lock expiration, and by keeping that expiration above the schedule interval. Read both
        // private constants and assert the ordering: lock-expiration >= 2x schedule, lock-expiration > timeout,
        // and run-budget < lock-expiration.
        var attribute = typeof(AgentAvailabilityRecoveryBackgroundTask)
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
        var field = typeof(AgentAvailabilityRecoveryBackgroundTask)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Static);

        return (int)field.GetRawConstantValue();
    }
}
