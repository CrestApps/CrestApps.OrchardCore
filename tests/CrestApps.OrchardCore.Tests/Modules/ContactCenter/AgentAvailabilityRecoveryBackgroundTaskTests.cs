using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityRecoveryBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_InvokesTenantRecoveryService()
    {
        // Arrange
        var recoveryService = new Mock<IAgentAvailabilityRecoveryService>();
        recoveryService
            .Setup(service => service.RecoverAsync(It.IsAny<CancellationToken>()))
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
            service => service.RecoverAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenTenantShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var recoveryService = new Mock<IAgentAvailabilityRecoveryService>();
        recoveryService
            .Setup(service => service.RecoverAsync(cancellationSource.Token))
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
}
