#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every event routing writes names who caused it. The agent a reservation is for is what the event is about, not
/// who made it: routing reserves and releases on its own, and only an accept is the agent's act.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task ReserveAsync_RecordsTheReservation_AsTheSystemsAct_AboutTheAgent()
    {
        // Arrange
        var log = new AuditedEventLog(FixedClock());
        var (service, item, agent) = CreateReservableService(log);

        // Act
        await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        foreach (var eventType in new[] { ContactCenterConstants.Events.QueueItemReserved, ContactCenterConstants.Events.AgentReserved })
        {
            var reserved = log.Single(eventType);
            Assert.Equal(ContactCenterActorType.System, reserved.ActorType);
            Assert.Equal(ContactCenterConstants.SystemActor, reserved.ActorId);

            // The agent is still named, as the subject: the reservation's payload says whose it is.
            var data = reserved.GetData<OfferLifecycleEventData>();
            Assert.NotNull(data);
            Assert.Equal("r1", data.ReservationId);
            Assert.Equal("a1", data.AgentId);
            Assert.Equal("u1", data.UserId);

            // Dated by the reservation itself, the same instant as the Reserved state it put the agent in.
            Assert.Equal(_now, reserved.OccurredUtc);
        }

        log.AssertEveryEventNamesItsActor();
    }

    [Fact]
    public async Task AcceptAsync_RecordsTheAssignment_AsTheAgentsAct()
    {
        // Arrange
        var log = new AuditedEventLog(FixedClock());
        var (service, item, agent) = CreateReservableService(log);
        await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Act
        await service.AcceptAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        var assigned = log.Single(ContactCenterConstants.Events.QueueItemAssigned);
        Assert.Equal(ContactCenterActorType.Agent, assigned.ActorType);
        Assert.Equal("u1", assigned.ActorId);
        Assert.Equal("a1", assigned.GetData<OfferLifecycleEventData>()?.AgentId);

        // The assignment and the Busy state it came with name the same actor.
        var busy = Assert.Single(log.Events, e => e.EventType == ContactCenterConstants.Events.AgentStateChanged &&
            e.GetData<AgentStateChangedEventData>()?.CurrentState == AgentPresenceStatus.Busy);
        Assert.Equal(busy.ActorType, assigned.ActorType);
        Assert.Equal(busy.ActorId, assigned.ActorId);

        log.AssertEveryEventNamesItsActor();
    }

    [Fact]
    public async Task ExpireDueAsync_RecordsTheRelease_AsTheSystemsAct()
    {
        // Arrange
        var log = new AuditedEventLog(FixedClock());
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.GetExpiredAsync(_now, It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpiredReservationPage([reservation], null, 0));
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Reserved, ActiveReservationId = "r1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateLoggedService(log, reservationManager, new Mock<IQueueItemManager>(), agentManager);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        var released = log.Single(ContactCenterConstants.Events.AgentReleased);
        Assert.Equal(ContactCenterActorType.System, released.ActorType);
        Assert.Equal("a1", released.GetData<OfferLifecycleEventData>()?.AgentId);
        Assert.Equal("u1", released.GetData<OfferLifecycleEventData>()?.UserId);
        log.AssertEveryEventNamesItsActor();
    }

    [Fact]
    public async Task CompensateAsync_RecordsTheRelease_AsTheSystemsAct()
    {
        // Arrange
        var log = new AuditedEventLog(FixedClock());
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Accepted);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        reservationManager.Setup(m => m.GetActiveByAgentAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Busy };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var service = CreateLoggedService(log, reservationManager, new Mock<IQueueItemManager>(), agentManager);

        // Act
        await service.CompensateAsync("r1", removeFromQueue: true, TestContext.Current.CancellationToken);

        // Assert
        var released = log.Single(ContactCenterConstants.Events.AgentReleased);
        Assert.Equal(ContactCenterActorType.System, released.ActorType);
        log.AssertEveryEventNamesItsActor();
    }

    [Fact]
    public async Task TheAgentsActivityTimeline_StillShowsTheReservationAndTheAssignment()
    {
        // Arrange
        // Routing's events stopped naming the agent as their actor. The agent's timeline finds its events by the
        // agent they are about -- the state changes by aggregate, the rest by payload -- so nothing may drop out.
        var log = new AuditedEventLog(FixedClock());
        var (service, item, agent) = CreateReservableService(log);
        await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);
        await service.AcceptAsync("r1", TestContext.Current.CancellationToken);

        // Act
        // Read the way the timeline report reads the log: agent state by the agent's aggregate, offers and calls by type.
        var timelines = AgentStateTimeline.Build(log.Events.Where(e =>
            e.AggregateType == nameof(AgentProfile) && AgentStateTimeline.StateEventTypes.Contains(e.EventType)));
        var offers = log.Events.Where(e => CallHandlingMetrics.OfferEventTypes.Contains(e.EventType));
        var calls = log.Events.Where(e => AgentActivityTimeline.CallEventTypes.Contains(e.EventType));
        var entries = AgentActivityTimeline.Build(timelines, [], offers, calls, _now.AddHours(-1), _now.AddHours(1));

        // Assert
        var timeline = Assert.Single(timelines);
        Assert.Equal("a1", timeline.AgentId);
        var states = entries.Where(entry => entry.Kind == AgentActivityKind.State).ToArray();
        var reserved = Assert.Single(states, entry => entry.Name == nameof(AgentPresenceStatus.Reserved));
        var busy = Assert.Single(states, entry => entry.Name == nameof(AgentPresenceStatus.Busy));
        Assert.Equal("a1", reserved.AgentId);
        Assert.Equal("int-1", reserved.InteractionId);
        Assert.Equal(ContactCenterActorType.System, reserved.ActorType);
        Assert.Equal("a1", busy.AgentId);
        Assert.Equal("int-1", busy.InteractionId);
        Assert.Equal(ContactCenterActorType.Agent, busy.ActorType);
        Assert.Equal("u1", busy.ActorId);
    }

    private static IClock FixedClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static (ActivityReservationService Service, QueueItem Item, AgentProfile Agent) CreateReservableService(AuditedEventLog log)
    {
        var reservation = new ActivityReservation { ItemId = "r1" };
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return (CreateLoggedService(log, reservationManager, queueItemManager, agentManager), item, agent);
    }

    private static ActivityReservationService CreateLoggedService(
        AuditedEventLog log,
        Mock<IActivityReservationManager> reservationManager,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        return CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            CreateInteractionManager("act-1", "int-1"),
            activityManager,
            log.PublisherMock,
            providerCommandStateService: null,
            scopeExecutor: new Mock<IContactCenterScopeExecutor>(),
            auditRecorder: log.Recorder);
    }
}
