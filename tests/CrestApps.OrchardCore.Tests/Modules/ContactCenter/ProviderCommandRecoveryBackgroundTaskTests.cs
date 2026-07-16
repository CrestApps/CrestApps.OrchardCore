using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCommandRecoveryBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_InvokesTenantProviderCommandRecovery()
    {
        // Arrange
        var processor = new Mock<IProviderCommandProcessor>();
        processor
            .Setup(service => service.RecoverDueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var services = new ServiceCollection();
        services.AddSingleton(processor.Object);
        services.AddSingleton<IContactCenterFeatureWorkManager>(new TestContactCenterFeatureWorkManager());
        services.AddSingleton(new Mock<ILogger<ProviderCommandRecoveryBackgroundTask>>().Object);
        await using var serviceProvider = services.BuildServiceProvider();

        // Act
        await new ProviderCommandRecoveryBackgroundTask()
            .DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        processor.Verify(
            service => service.RecoverDueAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenTenantShutdownCancels_PropagatesCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var processor = new Mock<IProviderCommandProcessor>();
        processor
            .Setup(service => service.RecoverDueAsync(cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));
        var services = new ServiceCollection();
        services.AddSingleton(processor.Object);
        services.AddSingleton<IContactCenterFeatureWorkManager>(new TestContactCenterFeatureWorkManager());
        services.AddSingleton(new Mock<ILogger<ProviderCommandRecoveryBackgroundTask>>().Object);
        await using var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ProviderCommandRecoveryBackgroundTask()
                .DoWorkAsync(serviceProvider, cancellationSource.Token));
    }
}
