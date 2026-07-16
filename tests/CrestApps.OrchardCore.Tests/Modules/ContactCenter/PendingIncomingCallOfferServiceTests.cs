using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class PendingIncomingCallOfferServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 9, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetForUserAsync_WhenPendingOfferExists_ReturnsRestorableIncomingOffer()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindPendingByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "reservation-1",
                ActivityItemId = "activity-1",
                QueueId = "queue-1",
                ExpiresUtc = _now.AddSeconds(30),
            });

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                AgentId = "agent-1",
                Direction = InteractionDirection.Inbound,
                Status = InteractionStatus.Ringing,
                ProviderInteractionId = "call-1",
                ProviderName = "Asterisk",
                CustomerAddress = "+15550001000",
                CreatedUtc = _now.AddSeconds(-5),
                TechnicalMetadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["serviceAddress"] = "+15550002000",
                },
            });

        var provider = new Mock<IIncomingCallContextProvider>();
        provider.Setup(p => p.ContributeAsync(It.IsAny<IncomingCallContributionContext>(), It.IsAny<CancellationToken>()))
            .Callback<IncomingCallContributionContext, CancellationToken>((context, _) =>
            {
                context.Heading = "Matched customers";
                context.Properties["acceptUrl"] = "/accept";
                context.Properties["declineUrl"] = "/decline";
                context.Cards.Add(new IncomingCallCard { Id = "card-1", Title = "Caller", Priority = 0 });
            })
            .Returns(Task.CompletedTask);

        var service = CreateService(agentManager, reservationManager, interactionManager, [provider.Object]);

        // Act
        var offer = await service.GetForUserAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(offer);
        Assert.Equal("call-1", offer.Call.CallId);
        Assert.Equal(CallDirection.Inbound, offer.Call.Direction);
        Assert.Equal(CallState.Ringing, offer.Call.State);
        Assert.Equal("+15550001000", offer.Call.From);
        Assert.Equal("+15550002000", offer.Call.To);
        Assert.Equal("Matched customers", offer.Context.Heading);
        Assert.Equal("/accept", offer.Context.Properties["acceptUrl"]);
        Assert.Equal("/decline", offer.Context.Properties["declineUrl"]);
        Assert.Equal("reservation-1", offer.Context.Properties["reservationId"]);
        Assert.Equal(_now.AddSeconds(30).ToString("O"), offer.Context.Properties["expiresUtc"]);
        Assert.Single(offer.Context.Cards);
    }

    [Fact]
    public async Task GetForUserAsync_WhenReservationExpired_ReturnsNull()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindPendingByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "reservation-1",
                ActivityItemId = "activity-1",
                ExpiresUtc = _now.AddSeconds(-1),
            });

        var interactionManager = new Mock<IInteractionManager>();
        var service = CreateService(agentManager, reservationManager, interactionManager, []);

        // Act
        var offer = await service.GetForUserAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(offer);
        interactionManager.Verify(manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetForUserAsync_WhenInteractionIsNotRinging_ReturnsNull()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(manager => manager.FindPendingByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "reservation-1",
                ActivityItemId = "activity-1",
                ExpiresUtc = _now.AddSeconds(30),
            });

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                AgentId = "agent-1",
                Direction = InteractionDirection.Inbound,
                Status = InteractionStatus.Created,
                ProviderInteractionId = "call-1",
            });

        var service = CreateService(agentManager, reservationManager, interactionManager, []);

        // Act
        var offer = await service.GetForUserAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(offer);
    }

    private static PendingIncomingCallOfferService CreateService(
        Mock<IAgentProfileManager> agentManager,
        Mock<IActivityReservationManager> reservationManager,
        Mock<IInteractionManager> interactionManager,
        IEnumerable<IIncomingCallContextProvider> providers)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new PendingIncomingCallOfferService(
            agentManager.Object,
            reservationManager.Object,
            interactionManager.Object,
            providers,
            clock.Object);
    }
}
