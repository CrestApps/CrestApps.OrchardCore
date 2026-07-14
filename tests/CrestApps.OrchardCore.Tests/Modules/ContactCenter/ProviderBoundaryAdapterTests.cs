using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderBoundaryAdapterTests
{
    [Fact]
    public async Task ProviderVoiceEventSink_WhenSessionMatches_ReturnsTrue()
    {
        // Arrange
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "provider",
            ProviderCallId = "call-1",
        };
        var service = new Mock<IProviderVoiceEventService>();
        service
            .Setup(value => value.IngestAsync(providerEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                ItemId = "session-1",
            });
        var sink = new ProviderVoiceEventSink(service.Object);

        // Act
        var handled = await sink.IngestAsync(providerEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
    }

    [Fact]
    public async Task ProviderVoiceEventSink_WhenNoSessionMatches_ReturnsFalse()
    {
        // Arrange
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "provider",
            ProviderCallId = "call-1",
        };
        var service = new Mock<IProviderVoiceEventService>();
        service
            .Setup(value => value.IngestAsync(providerEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        var sink = new ProviderVoiceEventSink(service.Object);

        // Act
        var handled = await sink.IngestAsync(providerEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public async Task ProviderCallStateReconciler_DelegatesProviderFilter()
    {
        // Arrange
        var service = new Mock<IProviderCallStateSynchronizationService>();
        service
            .Setup(value => value.ReconcileProviderInteractionsAsync("provider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var reconciler = new ProviderCallStateReconciler(service.Object);

        // Act
        var reconciled = await reconciler.ReconcileAsync("provider", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, reconciled);
    }

    [Fact]
    public async Task InboundVoiceEventSink_DelegatesNormalizedEvent()
    {
        // Arrange
        var inboundEvent = new InboundVoiceEvent
        {
            ProviderName = "provider",
            ProviderCallId = "call-1",
        };
        var router = new Mock<IVoiceContactCenterCallRouter>();
        router
            .Setup(value => value.RouteInboundAsync(inboundEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InboundVoiceRoutingResult());
        var sink = new InboundVoiceEventSink(router.Object);

        // Act
        await sink.RouteAsync(inboundEvent, TestContext.Current.CancellationToken);

        // Assert
        router.Verify(
            value => value.RouteInboundAsync(inboundEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
