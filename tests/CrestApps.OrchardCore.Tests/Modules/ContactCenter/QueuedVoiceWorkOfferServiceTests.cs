using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueuedVoiceWorkOfferServiceTests
{
    [Fact]
    public async Task OfferForAgentAsync_WhenAgentIsAvailable_OffersQueuedVoiceWorkUntilReserved()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["q1", "q2"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["q1", "q2"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Reserved,
                ActiveReservationId = "r1",
                QueueIds = ["q1", "q2"],
            });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");
        var session = new Mock<ISession>();

        var service = CreateService(agentManager, healer, inboundVoiceService, session);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, offered);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("q1", It.IsAny<CancellationToken>()), Times.Once);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("q2", It.IsAny<CancellationToken>()), Times.Never);
        session.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OfferForUserAsync_WhenAgentIsNotAvailable_DoesNotOfferQueuedVoiceWork()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Break,
                QueueIds = ["q1"],
            });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        var service = CreateService(agentManager, healer, inboundVoiceService, new Mock<ISession>());

        // Act
        var offered = await service.OfferForUserAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, offered);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OfferForAgentAsync_WhenAgentIsAvailable_RunsAvailabilityHealingBeforeOffering()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["q1"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["q1"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Reserved,
                ActiveReservationId = "r1",
                QueueIds = ["q1"],
            });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");

        var service = CreateService(agentManager, healer, inboundVoiceService, new Mock<ISession>());

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, offered);
        healer.Verify(manager => manager.HealForAvailabilityAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("q1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static QueuedVoiceWorkOfferService CreateService(
        Mock<IAgentProfileManager> agentManager,
        Mock<IAgentWorkStateHealingService> healer,
        Mock<IInboundVoiceService> inboundVoiceService,
        Mock<ISession> session)
    {
        return new QueuedVoiceWorkOfferService(
            agentManager.Object,
            [healer.Object],
            inboundVoiceService.Object,
            session.Object,
            Mock.Of<ILogger<QueuedVoiceWorkOfferService>>());
    }
}
