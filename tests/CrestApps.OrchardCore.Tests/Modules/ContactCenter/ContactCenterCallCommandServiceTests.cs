using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterCallCommandServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenAgentDeviceNativeProvider_RequiresDeviceAnswerAndDoesNotBridge()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupAcceptedReservation();
        harness.SetupInteraction();
        harness.SetupProvider(VoiceProviderDeliveryModel.AgentDeviceNative, ContactCenterVoiceProviderCapabilities.DialerDial);
        harness.SetupNewCallSession();

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresDeviceAnswer);
        Assert.Equal("int1", result.InteractionId);
        Assert.Equal(InteractionStatus.Connected, harness.Interaction.Status);
        Assert.Equal(_now, harness.Interaction.AnsweredUtc);

        harness.Provider.Verify(
            p => p.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        harness.CallSessionManager.Verify(
            m => m.CreateAsync(It.Is<CallSession>(s => s.State == ContactCenterCallState.Connected), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenServerSideAcdProvider_BridgesAndDoesNotRequireDeviceAnswer()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupAcceptedReservation();
        harness.SetupInteraction();
        harness.SetupProvider(
            VoiceProviderDeliveryModel.ServerSideAcd,
            ContactCenterVoiceProviderCapabilities.DialerDial | ContactCenterVoiceProviderCapabilities.AgentConnect);
        harness.SetupNewCallSession();

        harness.Provider
            .Setup(p => p.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-1" });

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        Assert.Equal(InteractionStatus.Connected, harness.Interaction.Status);

        harness.Provider.Verify(
            p => p.ConnectToAgentAsync(It.Is<ContactCenterConnectRequest>(r => r.AgentId == "a1" && r.InteractionId == "int1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenProviderConnectFails_DoesNotConnectInteraction()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupAcceptedReservation();
        harness.SetupInteraction();
        harness.SetupProvider(
            VoiceProviderDeliveryModel.ServerSideAcd,
            ContactCenterVoiceProviderCapabilities.DialerDial | ContactCenterVoiceProviderCapabilities.AgentConnect);

        harness.Provider
            .Setup(p => p.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = false, ErrorCode = "busy", ErrorMessage = "Agent device is busy." });

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Agent device is busy.", result.Reason);

        harness.InteractionManager.Verify(
            m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.CallSessionManager.Verify(
            m => m.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenReservationNoLongerAvailable_ReturnsFailure()
    {
        // Arrange
        var harness = new Harness();
        harness.ReservationService
            .Setup(s => s.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeclineInboundOfferAsync_RejectsAndReoffersQueue()
    {
        // Arrange
        var harness = new Harness();
        harness.ReservationService
            .Setup(s => s.RejectAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "a1", QueueId = "q1" });

        var service = harness.CreateService();

        // Act
        var result = await service.DeclineInboundOfferAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        harness.InboundVoiceService.Verify(s => s.OfferNextAsync("q1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Harness
    {
        public Mock<IActivityReservationService> ReservationService { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<ICallSessionManager> CallSessionManager { get; } = new();

        public Mock<IInboundVoiceService> InboundVoiceService { get; } = new();

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new();

        public Mock<IContactCenterVoiceProvider> Provider { get; } = new();

        public Interaction Interaction { get; } = new() { ItemId = "int1", ProviderName = "dp", ProviderInteractionId = "call-1", Direction = InteractionDirection.Inbound };

        public void SetupAcceptedReservation()
        {
            ReservationService
                .Setup(s => s.AcceptAsync("r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "a1", ActivityItemId = "act1", QueueId = "q1" });
        }

        public void SetupInteraction()
        {
            InteractionManager
                .Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            AgentManager
                .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "agent" });
        }

        public void SetupProvider(VoiceProviderDeliveryModel deliveryModel, ContactCenterVoiceProviderCapabilities capabilities)
        {
            Provider.SetupGet(p => p.TechnicalName).Returns("dp");
            Provider.SetupGet(p => p.DeliveryModel).Returns(deliveryModel);
            Provider.SetupGet(p => p.Capabilities).Returns(capabilities);

            VoiceProviderResolver
                .Setup(r => r.Get("dp"))
                .Returns(Provider.Object);
        }

        public void SetupNewCallSession()
        {
            CallSessionManager
                .Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallSession)null);

            CallSessionManager
                .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CallSession());
        }

        public ContactCenterCallCommandService CreateService()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ContactCenterCallCommandService>>();

            return new ContactCenterCallCommandService(
                ReservationService.Object,
                InteractionManager.Object,
                AgentManager.Object,
                VoiceProviderResolver.Object,
                CallSessionManager.Object,
                InboundVoiceService.Object,
                Publisher.Object,
                clock.Object,
                logger.Object);
        }
    }
}
