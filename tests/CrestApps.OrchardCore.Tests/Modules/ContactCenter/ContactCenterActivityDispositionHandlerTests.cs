using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterActivityDispositionHandlerTests
{
    [Fact]
    public async Task DispositionedAsync_WhenAssignedAgentIsInWrapUp_CompletesWorkAndOffersNextActivity()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.WrapUp,
            });

        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var offerService = new Mock<IQueuedVoiceWorkOfferService>();
        var interactionManager = new Mock<IInteractionManager>();
        var workStateService = new FakeContactCenterWorkStateService();
        workStateService.Seed(new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignedToId = "user-1",
        });

        var handler = new ContactCenterActivityDispositionHandler(
            agentManager.Object,
            presenceManager.Object,
            interactionManager.Object,
            workStateService,
            offerService.Object,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());

        var request = new ActivityDispositionRequest
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                AssignedToId = "user-1",
                Status = ActivityStatus.Completed,
            },
        };

        // Act
        await handler.DispositionedAsync(request, TestContext.Current.CancellationToken);

        // Assert
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
        offerService.Verify(
            service => service.OfferForAgentAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispositionedAsync_WhenAgentIsAlreadyAvailable_DoesNotChangePresence()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var presenceManager = new Mock<IAgentPresenceManager>();
        var offerService = new Mock<IQueuedVoiceWorkOfferService>();
        var interactionManager = new Mock<IInteractionManager>();
        var workStateService = new FakeContactCenterWorkStateService();
        workStateService.Seed(new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignedToId = "user-1",
        });

        var handler = new ContactCenterActivityDispositionHandler(
            agentManager.Object,
            presenceManager.Object,
            interactionManager.Object,
            workStateService,
            offerService.Object,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());

        var request = new ActivityDispositionRequest
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                AssignedToId = "user-1",
                Status = ActivityStatus.Completed,
            },
        };

        // Act
        await handler.DispositionedAsync(request, TestContext.Current.CancellationToken);

        // Assert
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        offerService.Verify(
            service => service.OfferForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispositionedAsync_WhenProviderEndIsStillPending_CompletesBusyAgentWork()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Busy,
            });

        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var offerService = new Mock<IQueuedVoiceWorkOfferService>();
        var interactionManager = new Mock<IInteractionManager>();
        var workStateService = new FakeContactCenterWorkStateService();
        workStateService.Seed(new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignedToId = "user-1",
        });

        var handler = new ContactCenterActivityDispositionHandler(
            agentManager.Object,
            presenceManager.Object,
            interactionManager.Object,
            workStateService,
            offerService.Object,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());

        var request = new ActivityDispositionRequest
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                AssignedToId = "user-1",
                Status = ActivityStatus.Completed,
            },
        };

        // Act
        await handler.DispositionedAsync(request, TestContext.Current.CancellationToken);

        // Assert
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
        offerService.Verify(
            service => service.OfferForAgentAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispositionedAsync_WhenAgentIsOnAnotherActivitysCall_EndsOnlyTheOldWrapUp()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Busy,
            });

        var oldInteraction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            AgentId = "agent-1",
            WrapUpStartedUtc = new DateTime(2026, 10, 27, 14, 54, 6, DateTimeKind.Utc),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldInteraction);
        interactionManager
            .Setup(manager => manager.FindActiveByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-2",
                ActivityItemId = "activity-2",
                AgentId = "agent-1",
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(new DateTime(2026, 10, 27, 14, 56, 35, DateTimeKind.Utc));

        var presenceManager = new Mock<IAgentPresenceManager>();
        var offerService = new Mock<IQueuedVoiceWorkOfferService>();
        var workStateService = new FakeContactCenterWorkStateService();
        workStateService.Seed(new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignedToId = "user-1",
        });

        var handler = new ContactCenterActivityDispositionHandler(
            agentManager.Object,
            presenceManager.Object,
            interactionManager.Object,
            workStateService,
            offerService.Object,
            clock.Object,
            Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());

        var request = new ActivityDispositionRequest
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                AssignedToId = "user-1",
                Status = ActivityStatus.Completed,
            },
        };

        // Act
        await handler.DispositionedAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 10, 27, 14, 56, 35, DateTimeKind.Utc), oldInteraction.WrapUpCompletedUtc);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        offerService.Verify(
            service => service.OfferForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispositionedAsync_WhenAgentsLiveCallIsThisActivity_CompletesWork()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Busy,
            });

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindActiveByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                AgentId = "agent-1",
            });

        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Available,
            });

        var offerService = new Mock<IQueuedVoiceWorkOfferService>();
        var workStateService = new FakeContactCenterWorkStateService();
        workStateService.Seed(new ContactCenterWorkState
        {
            ItemId = "work-state-1",
            ActivityItemId = "activity-1",
            AssignedToId = "user-1",
        });

        var handler = new ContactCenterActivityDispositionHandler(
            agentManager.Object,
            presenceManager.Object,
            interactionManager.Object,
            workStateService,
            offerService.Object,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());

        var request = new ActivityDispositionRequest
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                AssignedToId = "user-1",
                Status = ActivityStatus.Completed,
            },
        };

        // Act
        await handler.DispositionedAsync(request, TestContext.Current.CancellationToken);

        // Assert
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
        offerService.Verify(
            service => service.OfferForAgentAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
