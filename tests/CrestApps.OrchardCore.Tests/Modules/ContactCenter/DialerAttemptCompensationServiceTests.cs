using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerAttemptCompensationServiceTests
{
    [Fact]
    public void ComplianceStartup_RegistersAttemptAndCompensationServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new ComplianceStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IDialerAttemptService) &&
                descriptor.ImplementationType == typeof(DialerAttemptService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IDialerAttemptCompensationService) &&
                descriptor.ImplementationType == typeof(DialerAttemptCompensationService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task CompensateAsync_WhenQueueRemovalIsRequested_UsesAtomicReservationCompensation()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi1" };
        var reservationService = new Mock<IActivityReservationService>();
        var service = new DialerAttemptCompensationService(reservationService.Object);

        // Act
        await service.CompensateAsync(reservation, removeFromQueue: true, TestContext.Current.CancellationToken);

        // Assert
        reservationService.Verify(
            value => value.CompensateAsync(
                "r1",
                true,
                TestContext.Current.CancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CompensateAsync_WhenQueueRemovalIsNotRequested_OnlyCancelsReservation()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi1" };
        var reservationService = new Mock<IActivityReservationService>();
        var service = new DialerAttemptCompensationService(reservationService.Object);

        // Act
        await service.CompensateAsync(reservation, removeFromQueue: false, TestContext.Current.CancellationToken);

        // Assert
        reservationService.Verify(
            value => value.CompensateAsync(
                "r1",
                false,
                TestContext.Current.CancellationToken),
            Times.Once);
    }
}
