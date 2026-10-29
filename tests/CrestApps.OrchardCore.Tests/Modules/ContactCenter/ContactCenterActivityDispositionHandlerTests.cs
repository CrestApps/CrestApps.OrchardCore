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

    [Fact]
    public async Task DispositionedAsync_WhenAgentIsWrappingUpALaterActivitysCall_LeavesThatWrapUpAlone()
    {
        // Arrange: call A ended earlier; the agent took call B, which ended, and is wrapping B up.
        var interactionA = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            AgentId = "agent-1",
            CreatedUtc = new DateTime(2026, 10, 27, 14, 50, 0, DateTimeKind.Utc),
        };

        var interactionB = new Interaction
        {
            ItemId = "interaction-2",
            ActivityItemId = "activity-2",
            AgentId = "agent-1",
            CreatedUtc = new DateTime(2026, 10, 27, 14, 56, 0, DateTimeKind.Utc),
        };

        var fixture = new DispositionFixture(AgentPresenceStatus.WrapUp, interactionA, interactionB);

        // Act
        await fixture.DispositionAsync("activity-1");

        // Assert: a late disposition on A does not end B's wrap-up or offer the agent the next caller.
        fixture.PresenceManager.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.OfferService.Verify(
            service => service.OfferForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispositionedAsync_WhenAgentIsWrappingUpThisActivityAfterTransferringIt_CompletesWork()
    {
        // Arrange: the agent transferred this activity's call away, so it is no longer theirs; their newest interaction
        // is an older call of another activity. Their wrap-up is this activity's.
        var transferred = new Interaction
        {
            ItemId = "interaction-2",
            ActivityItemId = "activity-1",
            AgentId = "agent-2",
            CreatedUtc = new DateTime(2026, 10, 27, 14, 56, 0, DateTimeKind.Utc),
        };

        var olderCall = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-0",
            AgentId = "agent-1",
            CreatedUtc = new DateTime(2026, 10, 27, 14, 50, 0, DateTimeKind.Utc),
        };

        var fixture = new DispositionFixture(AgentPresenceStatus.WrapUp, transferred, olderCall);

        // Act
        await fixture.DispositionAsync("activity-1");

        // Assert
        fixture.PresenceManager.Verify(
            manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class DispositionFixture
    {
        private readonly ContactCenterActivityDispositionHandler _handler;

        public DispositionFixture(AgentPresenceStatus status, Interaction dispositioned, Interaction newestOfAgent)
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1", PresenceStatus = status });

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.FindByActivityIdAsync(dispositioned.ActivityItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dispositioned);
            interactionManager
                .Setup(manager => manager.GetRecentByAgentAsync("agent-1", 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync([newestOfAgent]);

            PresenceManager
                .Setup(manager => manager.CompleteWorkAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1", PresenceStatus = AgentPresenceStatus.Available });

            var workStateService = new FakeContactCenterWorkStateService();
            workStateService.Seed(new ContactCenterWorkState
            {
                ItemId = "work-state-1",
                ActivityItemId = dispositioned.ActivityItemId,
                AssignedToId = "user-1",
            });

            _handler = new ContactCenterActivityDispositionHandler(
                agentManager.Object,
                PresenceManager.Object,
                interactionManager.Object,
                workStateService,
                OfferService.Object,
                Mock.Of<IClock>(),
                Mock.Of<ILogger<ContactCenterActivityDispositionHandler>>());
        }

        public Mock<IAgentPresenceManager> PresenceManager { get; } = new();

        public Mock<IQueuedVoiceWorkOfferService> OfferService { get; } = new();

        public Task DispositionAsync(string activityId)
            => _handler.DispositionedAsync(new ActivityDispositionRequest
            {
                Activity = new OmnichannelActivity
                {
                    ItemId = activityId,
                    AssignedToId = "user-1",
                    Status = ActivityStatus.Completed,
                },
            }, TestContext.Current.CancellationToken);
    }
}
