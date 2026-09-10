using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using YesSql;

using OrchardCore.Modules;
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

    [Fact]
    public async Task OfferForAgentAsync_WhenHeldDirectCallTargetsAgent_OffersItEvenWithoutQueueMembership()
    {
        // Arrange: a direct-only agent with no queue membership, and a call held under the synthetic
        // direct-routing queue tagged for that agent.
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [],
            });

        var heldInteraction = new Interaction();
        heldInteraction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = "a1";

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldInteraction);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ItemId = "qi-1", ActivityItemId = "act-1", QueueId = ContactCenterConstants.DirectRouting.QueueId }]);

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferToAgentAsync("act-1", ContactCenterConstants.DirectRouting.QueueId, "a1", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");

        var service = CreateService(agentManager, healer, inboundVoiceService, new Mock<ISession>(), queueItemManager, interactionManager);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, offered);
        inboundVoiceService.Verify(
            voice => voice.OfferToAgentAsync("act-1", ContactCenterConstants.DirectRouting.QueueId, "a1", It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // The held direct call was taken; the agent (with no queue membership) is never pulled from a queue.
        inboundVoiceService.Verify(voice => voice.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OfferForAgentAsync_WhenCampaignQueueIsAutomatedPacedDial_DoesNotOfferFromIt()
    {
        // Arrange: an available agent signed into an outbound campaign queue whose head item is dialed by a
        // Power (automated pacing) profile. The pacing engine owns dialing these, so the offer scan must not
        // touch the queue - otherwise it reserves and immediately rejects the head item on every poll, churning
        // reservations and starving the pacing engine that actually places the call.
        var campaignQueueId = ContactCenterConstants.CampaignQueue.Prefix + "camp1";

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [campaignQueueId],
            });

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.FindNextWaitingAsync(campaignQueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ItemId = "qi1",
                ActivityItemId = "act1",
                QueueId = campaignQueueId,
                DialerProfileId = "prof-power",
            });

        var dialerProfileReader = new Mock<IDialerProfileReader>();
        dialerProfileReader
            .Setup(manager => manager.FindByIdAsync("prof-power", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "prof-power", Mode = DialerMode.Power });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();

        var service = CreateService(
            agentManager,
            healer,
            inboundVoiceService,
            new Mock<ISession>(),
            queueItemStore: queueItemStore,
            dialerProfileReader: dialerProfileReader);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, offered);
        inboundVoiceService.Verify(
            voice => voice.OfferNextAsync(campaignQueueId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OfferForAgentAsync_WhenCampaignQueueIsPreviewDial_StillOffersFromIt()
    {
        // Arrange: a campaign queue dialed by a Preview profile is agent-initiated, so the offer scan must
        // still offer from it - the fix must only skip the automated (paced) modes, not every campaign queue.
        var campaignQueueId = ContactCenterConstants.CampaignQueue.Prefix + "camp1";

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [campaignQueueId],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [campaignQueueId],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Reserved,
                ActiveReservationId = "r1",
                QueueIds = [campaignQueueId],
            });

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.FindNextWaitingAsync(campaignQueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ItemId = "qi1",
                ActivityItemId = "act1",
                QueueId = campaignQueueId,
                DialerProfileId = "prof-preview",
            });

        var dialerProfileReader = new Mock<IDialerProfileReader>();
        dialerProfileReader
            .Setup(manager => manager.FindByIdAsync("prof-preview", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "prof-preview", Mode = DialerMode.Preview });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync(campaignQueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");

        var service = CreateService(
            agentManager,
            healer,
            inboundVoiceService,
            new Mock<ISession>(),
            queueItemStore: queueItemStore,
            dialerProfileReader: dialerProfileReader);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, offered);
        inboundVoiceService.Verify(
            voice => voice.OfferNextAsync(campaignQueueId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OfferForAgentAsync_WhenPacedCampaignQueueHoldsTheOldestWork_StillOffersFromTheInboundQueueBehindIt()
    {
        // Arrange: the agent serves a Power-dialed campaign queue and an inbound support queue. The campaign
        // queue holds the contact who has waited longest, so the selector picks it first. Because the pacing
        // engine owns that queue, the pass must move on to the support queue rather than end with the agent
        // idle while an inbound caller waits.
        var campaignQueueId = ContactCenterConstants.CampaignQueue.Prefix + "camp1";
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.SetupSequence(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [campaignQueueId, "support"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [campaignQueueId, "support"],
            })
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Reserved,
                ActiveReservationId = "r1",
                QueueIds = [campaignQueueId, "support"],
            });

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.FindNextWaitingAsync(campaignQueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ItemId = "qi1",
                ActivityItemId = "act1",
                QueueId = campaignQueueId,
                DialerProfileId = "prof-power",
            });
        queueItemStore
            .Setup(store => store.GetHeadWaitingByQueueAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> queueIds, CancellationToken _) => queueIds
                .Select(queueId => new QueueItem
                {
                    ItemId = $"item-{queueId}",
                    QueueId = queueId,
                    EnqueuedUtc = queueId == campaignQueueId ? now.AddMinutes(-30) : now.AddMinutes(-1),
                })
                .ToArray());

        var dialerProfileReader = new Mock<IDialerProfileReader>();
        dialerProfileReader
            .Setup(manager => manager.FindByIdAsync("prof-power", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "prof-power", Mode = DialerMode.Power });

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync("support", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");

        var service = CreateService(
            agentManager,
            healer,
            inboundVoiceService,
            new Mock<ISession>(),
            queueItemStore: queueItemStore,
            dialerProfileReader: dialerProfileReader);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, offered);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync(campaignQueueId, It.IsAny<CancellationToken>()), Times.Never);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("support", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OfferForAgentAsync_WhenTheChosenQueueHasNothingThisAgentCanTake_TriesTheNextQueue()
    {
        // Arrange: the first queue the selector chooses offers nobody (a skill rule, or a race with another
        // node took the head item). Selecting again would pick the same queue forever, so the pass must
        // exclude it and give the next queue its turn - bounded, so an agent with no takeable work does not spin.
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AgentProfile
            {
                ItemId = "a1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["sales", "support"],
            });

        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.GetHeadWaitingByQueueAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> queueIds, CancellationToken _) => queueIds
                .Select(queueId => new QueueItem
                {
                    ItemId = $"item-{queueId}",
                    QueueId = queueId,
                    EnqueuedUtc = queueId == "sales" ? now.AddMinutes(-30) : now.AddMinutes(-1),
                })
                .ToArray());

        var healer = new Mock<IAgentWorkStateHealingService>();
        var inboundVoiceService = new Mock<IInboundVoiceService>();
        inboundVoiceService
            .Setup(service => service.OfferNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null);

        var service = CreateService(
            agentManager,
            healer,
            inboundVoiceService,
            new Mock<ISession>(),
            queueItemStore: queueItemStore);

        // Act
        var offered = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, offered);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("sales", It.IsAny<CancellationToken>()), Times.Once);
        inboundVoiceService.Verify(voice => voice.OfferNextAsync("support", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static QueuedVoiceWorkOfferService CreateService(
        Mock<IAgentProfileManager> agentManager,
        Mock<IAgentWorkStateHealingService> healer,
        Mock<IInboundVoiceService> inboundVoiceService,
        Mock<ISession> session,
        Mock<IQueueItemManager> queueItemManager = null,
        Mock<IInteractionManager> interactionManager = null,
        Mock<IQueueItemStore> queueItemStore = null,
        Mock<IDialerProfileReader> dialerProfileReader = null)
    {
        if (queueItemManager is null)
        {
            // Default to no held direct calls so tests that do not exercise that path behave as before. Only
            // applied when the caller did not supply its own mock, to avoid clobbering an explicit setup.
            queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager
                .Setup(manager => manager.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        // The selector reads the head of each queue the agent serves and the queue that owns it. Tests that do
        // not care which queue is chosen still need both to answer, or the selector correctly finds no work and
        // the offer never happens.
        var resolvedQueueItemStore = queueItemStore ?? new Mock<IQueueItemStore>();

        resolvedQueueItemStore
            .Setup(store => store.GetHeadWaitingByQueueAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> queueIds, CancellationToken _) => queueIds
                .Select(queueId => new QueueItem
                {
                    ItemId = $"item-{queueId}",
                    QueueId = queueId,
                })
                .ToArray());

        // The catalog behaves like the real one: a virtual campaign queue is never stored, so it is not found.
        // The selector must resolve those itself, and a test that hid that behind an always-found stub would
        // have missed the regression that signed every campaign agent out of their campaign work.
        var selectorQueueManager = new Mock<IActivityQueueManager>();
        selectorQueueManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string queueId, CancellationToken _) => ContactCenterConstants.IsCampaignQueue(queueId)
                ? null
                : new ActivityQueue { ItemId = queueId });

        return new QueuedVoiceWorkOfferService(
            agentManager.Object,
            healer.Object,
            inboundVoiceService.Object,
            queueItemManager.Object,
            resolvedQueueItemStore.Object,
            (interactionManager ?? new Mock<IInteractionManager>()).Object,
            dialerProfileReader?.Object ?? new NullDialerProfileReader(),
            // The real selector over the real store, so these tests exercise the cross-queue choice rather
            // than a stub of it.
            new AgentWorkSelector(resolvedQueueItemStore.Object, selectorQueueManager.Object, Mock.Of<IClock>()),
            new FakeDistributedLock(),
            CoordinationOptions(),
            session.Object,
            Mock.Of<ILogger<QueuedVoiceWorkOfferService>>());
    }

    // The coordination timings are options now, so a test uses the shipped defaults rather than a value it
    // invents that no deployment would run with.
    private static OptionsWrapper<ContactCenterCoordinationOptions> CoordinationOptions()
        => new OptionsWrapper<ContactCenterCoordinationOptions>(new ContactCenterCoordinationOptions());
}
